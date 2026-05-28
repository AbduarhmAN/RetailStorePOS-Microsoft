-- ---------------------------------------------------------------------------
-- license-api hardening
-- Adds:
--   * license_api_rate_limit       — per-IP sliding-window counters used by
--                                    utils/rate-limit.ts
--   * claim_license_seat()         — atomic version of the previous
--                                    SELECT count + INSERT pattern in
--                                    handlers/verify-license.ts. Closes the
--                                    TOCTOU race that allowed two parallel
--                                    activations to both grab the last seat.
--   * refresh_license_activation() — refresh path used in the same handler.
--                                    Refuses to resurrect activations whose
--                                    status is 'revoked' (terminal state).
--
-- The Edge Function calls these via supabaseAdmin.rpc() so all the row locking
-- happens server-side in a single transaction.
-- ---------------------------------------------------------------------------

create table if not exists public.license_api_rate_limit (
    bucket_key   text        primary key,
    window_start timestamptz not null,
    request_count int        not null default 0,
    updated_at   timestamptz not null default timezone('utc'::text, now())
);

comment on table public.license_api_rate_limit is
    'Per-IP request counters for the license-api Edge Function. Rows older than the active window are eligible for cleanup.';

alter table public.license_api_rate_limit enable row level security;

create index if not exists idx_license_api_rate_limit_window
    on public.license_api_rate_limit(window_start);

-- ---------------------------------------------------------------------------
-- claim_license_seat: atomic activation creation that respects max_devices.
--
-- Locks the licenses row, counts active activations, and either inserts a new
-- activation row or returns 'max_devices_reached' without inserting. Uses the
-- existing unique(license_id, install_id) constraint to prevent duplicate seats
-- per install (the function only inserts when the row does not yet exist).
-- ---------------------------------------------------------------------------
create or replace function public.claim_license_seat(
    p_license_id uuid,
    p_install_id text,
    p_max_devices int,
    p_device_public_key_thumbprint text,
    p_request_sequence bigint,
    p_now timestamptz
)
returns table (
    activation_id uuid,
    activation_status text,
    last_request_sequence bigint,
    activated_at timestamptz,
    result_code text
)
language plpgsql
security definer
set search_path = public
as $$
declare
    v_existing public.license_activations;
    v_active_count int;
    v_inserted public.license_activations;
begin
    -- Lock the licenses row so two parallel activations cannot both pass the
    -- count check on the last seat.
    perform 1
    from public.licenses
    where id = p_license_id
    for update;

    select * into v_existing
    from public.license_activations
    where license_id = p_license_id
      and install_id = p_install_id
    limit 1;

    if v_existing.id is not null then
        -- Same install reactivating: not a new seat. Refresh path handles it.
        return query select
            v_existing.id,
            v_existing.status,
            v_existing.last_request_sequence,
            v_existing.activated_at,
            'existing'::text;
        return;
    end if;

    select count(*)::int into v_active_count
    from public.license_activations
    where license_id = p_license_id
      and status = 'active';

    if v_active_count >= p_max_devices then
        return query select
            null::uuid,
            null::text,
            null::bigint,
            null::timestamptz,
            'max_devices_reached'::text;
        return;
    end if;

    insert into public.license_activations (
        license_id,
        install_id,
        status,
        device_public_key_thumbprint,
        last_request_sequence,
        activated_at,
        last_refreshed_at,
        updated_at
    )
    values (
        p_license_id,
        p_install_id,
        'active',
        p_device_public_key_thumbprint,
        coalesce(p_request_sequence, 0),
        p_now,
        p_now,
        p_now
    )
    returning * into v_inserted;

    return query select
        v_inserted.id,
        v_inserted.status,
        v_inserted.last_request_sequence,
        v_inserted.activated_at,
        'created'::text;
end;
$$;

comment on function public.claim_license_seat is
    'Atomically reserves a license seat for an install. Returns result_code one of (created|existing|max_devices_reached).';

-- ---------------------------------------------------------------------------
-- refresh_license_activation: idempotent refresh path that refuses to
-- resurrect activations whose status is 'revoked' (terminal state).
-- ---------------------------------------------------------------------------
create or replace function public.refresh_license_activation(
    p_activation_id uuid,
    p_device_public_key_thumbprint text,
    p_request_sequence bigint,
    p_now timestamptz
)
returns table (
    activation_id uuid,
    activation_status text,
    last_request_sequence bigint,
    activated_at timestamptz,
    result_code text
)
language plpgsql
security definer
set search_path = public
as $$
declare
    v_row public.license_activations;
begin
    select * into v_row
    from public.license_activations
    where id = p_activation_id
    for update;

    if v_row.id is null then
        return query select null::uuid, null::text, null::bigint, null::timestamptz, 'not_found'::text;
        return;
    end if;

    if v_row.status = 'revoked' then
        -- Terminal state: do not allow refresh to flip status back to active.
        return query select
            v_row.id,
            v_row.status,
            v_row.last_request_sequence,
            v_row.activated_at,
            'activation_revoked'::text;
        return;
    end if;

    update public.license_activations as la
    set
        status = 'active',
        device_public_key_thumbprint = coalesce(p_device_public_key_thumbprint, la.device_public_key_thumbprint),
        last_request_sequence = greatest(la.last_request_sequence, coalesce(p_request_sequence, la.last_request_sequence)),
        last_refreshed_at = p_now,
        updated_at = p_now
    where la.id = p_activation_id
    returning * into v_row;

    return query select
        v_row.id,
        v_row.status,
        v_row.last_request_sequence,
        v_row.activated_at,
        'refreshed'::text;
end;
$$;

comment on function public.refresh_license_activation is
    'Refreshes an existing license activation row. Returns result_code one of (not_found|activation_revoked|refreshed). Will not reactivate a row whose status is "revoked".';

grant execute on function public.claim_license_seat(uuid, text, int, text, bigint, timestamptz) to service_role;
grant execute on function public.refresh_license_activation(uuid, text, bigint, timestamptz) to service_role;
