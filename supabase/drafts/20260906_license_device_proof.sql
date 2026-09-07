begin;

create table public.license_device_bindings (
    install_id text primary key references public.installations(install_id),
    license_id uuid not null references public.licenses(id),
    public_key jsonb not null,
    thumbprint text not null,
    recovery_authorized boolean not null default false,
    updated_at timestamptz not null default now()
);
create table public.license_device_challenges (
    id uuid primary key default gen_random_uuid(),
    install_id text not null,
    license_id uuid not null references public.licenses(id),
    public_key jsonb not null,
    thumbprint text not null,
    payload jsonb not null,
    expires_at timestamptz not null,
    consumed_at timestamptz
);
create index license_device_challenges_install_expiry
    on public.license_device_challenges(install_id, expires_at);
create table public.license_device_rate_limits (
    bucket text primary key,
    window_start timestamptz not null,
    request_count integer not null
);
alter table public.license_device_bindings enable row level security;
alter table public.license_device_challenges enable row level security;
alter table public.license_device_rate_limits enable row level security;
revoke all on public.license_device_bindings, public.license_device_challenges,
    public.license_device_rate_limits from public, anon, authenticated;
grant all on public.license_device_bindings, public.license_device_challenges,
    public.license_device_rate_limits to service_role;

create function public.license_device_rate_limit(p_bucket text) returns boolean
language plpgsql security definer set search_path = '' as $$
declare v_count integer;
begin
    insert into public.license_device_rate_limits as r values (p_bucket, now(), 1)
    on conflict (bucket) do update set
        request_count = case when r.window_start < now() - interval '1 minute' then 1 else r.request_count + 1 end,
        window_start = case when r.window_start < now() - interval '1 minute' then now() else r.window_start end
    returning request_count into v_count;
    return v_count <= 12;
end;
$$;

create function public.begin_license_device_challenge(
    p_install_id text, p_mode text, p_license_hash text, p_public_key jsonb,
    p_thumbprint text, p_request_nonce text, p_sequence bigint
) returns jsonb language plpgsql security definer set search_path = '' as $$
declare
    v_install text;
    v_binding public.license_device_bindings;
    v_license public.licenses;
    v_id uuid := gen_random_uuid();
    v_expires timestamptz := now() + interval '2 minutes';
    v_nonce text := encode(gen_random_bytes(32), 'hex');
    v_sequence bigint;
    v_payload jsonb;
begin
    if p_mode is null or p_mode not in ('enroll','refresh') or p_sequence < 1 or p_sequence > 9007199254740991
       or p_sequence is null or p_thumbprint is null or length(p_thumbprint) <> 43
        or p_request_nonce is null or p_request_nonce !~ '^[0-9a-f]{32}$' then
        raise exception 'invalid_request';
    end if;
    v_install := public.resolve_canonical_install_id(p_install_id);
    perform pg_advisory_xact_lock(hashtextextended(v_install, 602));
    select * into v_binding from public.license_device_bindings where install_id = v_install for update;
    if p_mode = 'refresh' then
        if v_binding.install_id is null then raise exception 'device_not_enrolled'; end if;
        if v_binding.recovery_authorized or v_binding.thumbprint <> p_thumbprint then
            raise exception 'device_key_mismatch';
        end if;
        select * into v_license from public.licenses where id = v_binding.license_id;
    else
        select * into v_license from public.licenses
        where license_key_hash = p_license_hash and product_code = 'RETAILSTOREPOS';
        if v_binding.install_id is not null and not v_binding.recovery_authorized
           and (v_binding.thumbprint <> p_thumbprint or v_binding.license_id <> v_license.id) then
            raise exception 'device_recovery_required';
        end if;
    end if;
    if v_license.id is null then raise exception 'license_not_found'; end if;
    if v_license.status not in ('active','trial') then raise exception 'license_inactive'; end if;
    if v_license.starts_at > now() or v_license.expires_at <= now() then raise exception 'license_expired'; end if;
    select greatest(p_sequence, coalesce(max(a.last_request_sequence),0) + 1) into v_sequence
    from public.license_activations a where a.install_id = v_install;
    if v_sequence > 9007199254740991 then raise exception 'sequence_exhausted'; end if;
    if (select count(*) from public.license_device_challenges
        where install_id = v_install and expires_at > now()) >= 5 then raise exception 'rate_limited'; end if;
    v_payload := jsonb_build_object(
        'audience','nexill-license-device-v2',
        'messageType','license_device_challenge','protocolVersion',2,'challengeId',v_id,
        'installId',v_install,'productCode','RETAILSTOREPOS','mode',p_mode,
        'devicePublicKeyThumbprint',p_thumbprint,'nonce',v_nonce,
        'requestNonce',p_request_nonce,'requestSequence',v_sequence,
        'issuedAtUtc',to_char(now() at time zone 'UTC','YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'),
        'expiresAtUtc',to_char(v_expires at time zone 'UTC','YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'));
    insert into public.license_device_challenges
        (id,install_id,license_id,public_key,thumbprint,payload,expires_at)
    values (v_id,v_install,v_license.id,p_public_key,p_thumbprint,v_payload,v_expires);
    return v_payload;
end;
$$;

-- Both deployed v1 entry points refresh through these table writes. Enrolled
-- installs must use the verified v2 transaction, even if they still know a key.
create function public.guard_license_device_binding() returns trigger
language plpgsql security definer set search_path = '' as $$
begin
    perform pg_advisory_xact_lock(hashtextextended(new.install_id, 602));
    if exists (select 1 from public.license_device_bindings where install_id = new.install_id)
       and coalesce(current_setting('nexill.license_device_verified', true),'') <> '1' then
        -- Administrators can still revoke without granting or refreshing access.
        if tg_op = 'UPDATE' and new.status in ('revoked','suspended')
           and new.install_id = old.install_id and new.license_id = old.license_id
           and new.last_request_sequence = old.last_request_sequence
           and new.device_public_key_thumbprint is not distinct from old.device_public_key_thumbprint then
            return new;
        end if;
        raise exception 'device_proof_required';
    end if;
    return new;
end;
$$;
create trigger license_device_binding_guard before insert or update on public.license_activations
for each row execute function public.guard_license_device_binding();

create function public.complete_license_device_challenge(
    p_challenge_id uuid, p_expected_thumbprint text, p_expected_sequence bigint
) returns jsonb
language plpgsql security definer set search_path = '' as $$
declare
    v_challenge public.license_device_challenges;
    v_binding public.license_device_bindings;
    v_license public.licenses;
    v_activation public.license_activations;
    v_features jsonb;
    v_expiry timestamptz;
    v_sequence bigint;
begin
    -- Only service_role may call this after Edge Function signature verification.
    select * into v_challenge from public.license_device_challenges where id = p_challenge_id for update;
    if v_challenge.id is null or v_challenge.consumed_at is not null or v_challenge.expires_at <= now() then
        raise exception 'challenge_expired_or_used';
    end if;
    -- Bind the transaction to the exact challenge verified by the Edge Function.
    if v_challenge.thumbprint is distinct from p_expected_thumbprint
       or (v_challenge.payload->>'requestSequence')::bigint is distinct from p_expected_sequence then
        raise exception 'verified_challenge_mismatch';
    end if;
    perform pg_advisory_xact_lock(hashtextextended(v_challenge.install_id, 602));
    select * into v_license from public.licenses where id = v_challenge.license_id for update;
    if v_license.status not in ('active','trial') then raise exception 'license_inactive'; end if;
    if v_license.starts_at > now() or v_license.expires_at <= now() then raise exception 'license_expired'; end if;
    if not exists(select 1 from public.permission_groups where product_code=v_license.product_code
        and permission_group=v_license.permission_group and status='active') then
        raise exception 'permission_group_not_active';
    end if;
    select * into v_binding from public.license_device_bindings where install_id=v_challenge.install_id for update;
    if v_binding.install_id is not null and
       (v_binding.thumbprint <> v_challenge.thumbprint or v_binding.license_id <> v_challenge.license_id
        or v_binding.recovery_authorized) then
        if not (v_binding.recovery_authorized and v_challenge.payload->>'mode'='enroll') then
            raise exception 'device_key_mismatch';
        end if;
    end if;
    select * into v_activation from public.license_activations
    where license_id=v_license.id and install_id=v_challenge.install_id for update;
    if v_activation.id is not null and v_activation.status not in ('active','trial') then
        raise exception 'activation_revoked';
    end if;
    v_sequence := (v_challenge.payload->>'requestSequence')::bigint;
    if v_activation.id is not null and v_sequence <= v_activation.last_request_sequence then
        raise exception 'request_replayed';
    end if;
    if v_activation.id is null and (select count(*) from public.license_activations
        where license_id=v_license.id and status in ('active','trial')) >= v_license.max_devices then
        raise exception 'max_devices_reached';
    end if;
    perform set_config('nexill.license_device_verified','1',true);
    insert into public.installations(install_id,type) values(v_challenge.install_id,'auto_activate')
        on conflict(install_id) do nothing;
    if v_activation.id is null then
        insert into public.license_activations(license_id,install_id,status,device_public_key_thumbprint,
            last_request_sequence,activated_at,last_refreshed_at,updated_at)
        values(v_license.id,v_challenge.install_id,'active',v_challenge.thumbprint,v_sequence,now(),now(),now())
        returning * into v_activation;
    else
        update public.license_activations set device_public_key_thumbprint=v_challenge.thumbprint,
            last_request_sequence=v_sequence,last_refreshed_at=now(),updated_at=now()
        where id=v_activation.id returning * into v_activation;
    end if;
    insert into public.license_device_bindings as b(install_id,license_id,public_key,thumbprint)
    values(v_challenge.install_id,v_license.id,v_challenge.public_key,v_challenge.thumbprint)
    on conflict(install_id) do update set license_id=excluded.license_id,public_key=excluded.public_key,
        thumbprint=excluded.thumbprint,recovery_authorized=false,updated_at=now();
    update public.license_device_challenges set consumed_at=now() where id=p_challenge_id;
    select coalesce(jsonb_agg(feature_code order by feature_code),'[]'::jsonb) into v_features
    from public.permission_group_features where product_code=v_license.product_code
        and permission_group=v_license.permission_group and enabled=true;
    v_expiry := least(now()+interval '30 days',coalesce(v_license.expires_at,'infinity'::timestamptz));
    return jsonb_build_object('messageType','license_activation_certificate','certificateVersion',2,
        'activationCertificateId',gen_random_uuid(),'licenseId',v_license.id,'activationId',v_activation.id,
        'installId',v_challenge.install_id,'productCode',v_license.product_code,
        'permissionGroup',v_license.permission_group,'features',v_features,
        'licenseStatus',v_license.status,'activationStatus',v_activation.status,
        'issuedAtUtc',to_char(now() at time zone 'UTC','YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'),
        'notBeforeUtc',to_char(now() at time zone 'UTC','YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'),
        'expiresAtUtc',to_char(v_expiry at time zone 'UTC','YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'),
        'requestNonce',v_challenge.payload->>'requestNonce','requestSequence',v_sequence,
        'devicePublicKeyThumbprint',v_challenge.thumbprint);
end;
$$;

revoke all on function public.license_device_rate_limit(text),
    public.begin_license_device_challenge(text,text,text,jsonb,text,text,bigint),
    public.complete_license_device_challenge(uuid,text,bigint),public.guard_license_device_binding()
    from public,anon,authenticated;
grant execute on function public.license_device_rate_limit(text),
    public.begin_license_device_challenge(text,text,text,jsonb,text,text,bigint),
    public.complete_license_device_challenge(uuid,text,bigint) to service_role;
commit;
