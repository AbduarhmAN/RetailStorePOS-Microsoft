create table if not exists public.install_id_aliases (
    old_install_id text primary key,
    canonical_install_id text not null,
    migration_source text null,
    created_at timestamptz not null default timezone('utc', now()),
    updated_at timestamptz not null default timezone('utc', now()),
    constraint install_id_aliases_old_not_blank check (btrim(old_install_id) <> ''),
    constraint install_id_aliases_canonical_not_blank check (btrim(canonical_install_id) <> ''),
    constraint install_id_aliases_ids_differ check (old_install_id <> canonical_install_id)
);

create index if not exists idx_install_id_aliases_canonical_install_id
    on public.install_id_aliases(canonical_install_id);

create or replace function public.resolve_canonical_install_id(p_install_id text)
returns text
language plpgsql
as $$
declare
    v_current_install_id text := nullif(btrim(p_install_id), '');
    v_next_install_id text;
    v_hop_count integer := 0;
begin
    if v_current_install_id is null then
        return p_install_id;
    end if;

    loop
        select alias.canonical_install_id
        into v_next_install_id
        from public.install_id_aliases alias
        where alias.old_install_id = v_current_install_id;

        exit when v_next_install_id is null
            or btrim(v_next_install_id) = ''
            or v_next_install_id = v_current_install_id;

        v_current_install_id := v_next_install_id;
        v_hop_count := v_hop_count + 1;

        exit when v_hop_count >= 16;
    end loop;

    return v_current_install_id;
end;
$$;

create or replace function public.reconcile_install_id_alias(
    p_old_install_id text,
    p_canonical_install_id text,
    p_migration_source text default 'manual'
)
returns void
language plpgsql
as $$
declare
    v_old_install_id text := nullif(btrim(p_old_install_id), '');
    v_canonical_install_id text := nullif(btrim(p_canonical_install_id), '');
    v_now timestamptz := timezone('utc', now());
begin
    if v_old_install_id is null or v_canonical_install_id is null then
        return;
    end if;

    v_canonical_install_id := public.resolve_canonical_install_id(v_canonical_install_id);
    v_old_install_id := public.resolve_canonical_install_id(v_old_install_id);

    if v_old_install_id is null
       or v_canonical_install_id is null
       or v_old_install_id = v_canonical_install_id then
        return;
    end if;

    insert into public.install_id_aliases (
        old_install_id,
        canonical_install_id,
        migration_source,
        created_at,
        updated_at
    )
    values (
        v_old_install_id,
        v_canonical_install_id,
        p_migration_source,
        v_now,
        v_now
    )
    on conflict (old_install_id) do update
    set canonical_install_id = excluded.canonical_install_id,
        migration_source = excluded.migration_source,
        updated_at = excluded.updated_at;

    update public.install_id_aliases
    set canonical_install_id = v_canonical_install_id,
        migration_source = coalesce(p_migration_source, migration_source),
        updated_at = v_now
    where canonical_install_id = v_old_install_id;

    if to_regclass('public.installations') is not null then
        insert into public.installations (
            install_id,
            type,
            run_id,
            app_version,
            timestamp,
            windows_version,
            os_architecture,
            dotnet_runtime_version,
            ram_bucket,
            cpu_core_bucket,
            screen_resolution_bucket,
            build_number,
            release_channel,
            first_run_at,
            last_seen_at,
            days_since_install,
            days_since_last_seen,
            country_setting,
            currency_setting,
            startup_time_bucket_ms,
            feature_flags,
            last_update_prompt_at,
            last_update_installed_at,
            crash_count,
            internet_status,
            app_launched_count,
            created_at,
            updated_at
        )
        select
            v_canonical_install_id,
            source.type,
            source.run_id,
            source.app_version,
            source.timestamp,
            source.windows_version,
            source.os_architecture,
            source.dotnet_runtime_version,
            source.ram_bucket,
            source.cpu_core_bucket,
            source.screen_resolution_bucket,
            source.build_number,
            source.release_channel,
            source.first_run_at,
            source.last_seen_at,
            source.days_since_install,
            source.days_since_last_seen,
            source.country_setting,
            source.currency_setting,
            source.startup_time_bucket_ms,
            source.feature_flags,
            source.last_update_prompt_at,
            source.last_update_installed_at,
            source.crash_count,
            source.internet_status,
            source.app_launched_count,
            source.created_at,
            source.updated_at
        from public.installations source
        where source.install_id = v_old_install_id
        on conflict (install_id) do nothing;

        update public.installations target
        set type = coalesce(target.type, source.type),
            run_id = coalesce(target.run_id, source.run_id),
            app_version = coalesce(target.app_version, source.app_version),
            timestamp = coalesce(greatest(target.timestamp, source.timestamp), target.timestamp, source.timestamp),
            windows_version = coalesce(target.windows_version, source.windows_version),
            os_architecture = coalesce(target.os_architecture, source.os_architecture),
            dotnet_runtime_version = coalesce(target.dotnet_runtime_version, source.dotnet_runtime_version),
            ram_bucket = coalesce(target.ram_bucket, source.ram_bucket),
            cpu_core_bucket = coalesce(target.cpu_core_bucket, source.cpu_core_bucket),
            screen_resolution_bucket = coalesce(target.screen_resolution_bucket, source.screen_resolution_bucket),
            build_number = coalesce(target.build_number, source.build_number),
            release_channel = coalesce(target.release_channel, source.release_channel),
            first_run_at = coalesce(least(target.first_run_at, source.first_run_at), target.first_run_at, source.first_run_at),
            last_seen_at = coalesce(greatest(target.last_seen_at, source.last_seen_at), target.last_seen_at, source.last_seen_at),
            days_since_install = coalesce(target.days_since_install, source.days_since_install),
            days_since_last_seen = coalesce(target.days_since_last_seen, source.days_since_last_seen),
            country_setting = coalesce(target.country_setting, source.country_setting),
            currency_setting = coalesce(target.currency_setting, source.currency_setting),
            startup_time_bucket_ms = coalesce(target.startup_time_bucket_ms, source.startup_time_bucket_ms),
            feature_flags = coalesce(target.feature_flags, source.feature_flags),
            last_update_prompt_at = coalesce(greatest(target.last_update_prompt_at, source.last_update_prompt_at), target.last_update_prompt_at, source.last_update_prompt_at),
            last_update_installed_at = coalesce(greatest(target.last_update_installed_at, source.last_update_installed_at), target.last_update_installed_at, source.last_update_installed_at),
            crash_count = greatest(coalesce(target.crash_count, 0), coalesce(source.crash_count, 0)),
            internet_status = coalesce(target.internet_status, source.internet_status),
            app_launched_count = greatest(coalesce(target.app_launched_count, 0), coalesce(source.app_launched_count, 0)),
            created_at = least(target.created_at, source.created_at),
            updated_at = coalesce(greatest(target.updated_at, source.updated_at), target.updated_at, source.updated_at)
        from public.installations source
        where target.install_id = v_canonical_install_id
          and source.install_id = v_old_install_id;
    end if;

    if to_regclass('public.license_activations') is not null then
        update public.license_activations target
        set status = case
                when target.status = 'active' or source.status = 'active' then 'active'
                when target.status = 'trial' or source.status = 'trial' then coalesce(target.status, source.status)
                else coalesce(target.status, source.status)
            end,
            device_public_key_thumbprint = coalesce(target.device_public_key_thumbprint, source.device_public_key_thumbprint),
            last_request_sequence = greatest(coalesce(target.last_request_sequence, 0), coalesce(source.last_request_sequence, 0)),
            activated_at = coalesce(least(target.activated_at, source.activated_at), target.activated_at, source.activated_at),
            last_refreshed_at = coalesce(greatest(target.last_refreshed_at, source.last_refreshed_at), target.last_refreshed_at, source.last_refreshed_at),
            revoked_at = coalesce(greatest(target.revoked_at, source.revoked_at), target.revoked_at, source.revoked_at),
            revoke_reason = coalesce(target.revoke_reason, source.revoke_reason),
            updated_at = coalesce(greatest(target.updated_at, source.updated_at), target.updated_at, source.updated_at)
        from public.license_activations source
        where target.install_id = v_canonical_install_id
          and source.install_id = v_old_install_id
          and target.license_id = source.license_id;

        delete from public.license_activations source
        using public.license_activations target
        where source.install_id = v_old_install_id
          and target.install_id = v_canonical_install_id
          and target.license_id = source.license_id;

        update public.license_activations
        set install_id = v_canonical_install_id,
            updated_at = v_now
        where install_id = v_old_install_id;
    end if;

    if to_regclass('public.installation_events') is not null then
        update public.installation_events
        set install_id = v_canonical_install_id
        where install_id = v_old_install_id;
    end if;

    if to_regclass('public.error_logs') is not null then
        update public.error_logs
        set install_id = v_canonical_install_id
        where install_id = v_old_install_id;
    end if;

    if to_regclass('public.installations') is not null then
        delete from public.installations
        where install_id = v_old_install_id;
    end if;
end;
$$;

create or replace function public.canonicalize_install_id_reference()
returns trigger
language plpgsql
as $$
declare
    v_canonical_install_id text;
begin
    if new.install_id is null or btrim(new.install_id) = '' then
        return new;
    end if;

    v_canonical_install_id := public.resolve_canonical_install_id(new.install_id);
    if v_canonical_install_id is not null and btrim(v_canonical_install_id) <> '' then
        new.install_id := v_canonical_install_id;
    end if;

    return new;
end;
$$;

create or replace function public.reconcile_install_id_migration_event()
returns trigger
language plpgsql
as $$
declare
    v_payload jsonb;
    v_previous_install_id text;
begin
    if new.event_type <> 'install_id_migrated' then
        return new;
    end if;

    begin
        v_payload := new.payload_json::jsonb;
    exception when others then
        return new;
    end;

    v_previous_install_id := nullif(btrim(v_payload ->> 'previous_install_id'), '');
    if v_previous_install_id is null or v_previous_install_id = new.install_id then
        return new;
    end if;

    perform public.reconcile_install_id_alias(
        v_previous_install_id,
        new.install_id,
        'installation_event'
    );

    return new;
end;
$$;

do $$
begin
    if to_regclass('public.installations') is not null then
        drop trigger if exists trg_installations_canonicalize_install_id on public.installations;
        create trigger trg_installations_canonicalize_install_id
        before insert or update of install_id on public.installations
        for each row
        execute function public.canonicalize_install_id_reference();
    end if;

    if to_regclass('public.installation_events') is not null then
        drop trigger if exists trg_installation_events_canonicalize_install_id on public.installation_events;
        create trigger trg_installation_events_canonicalize_install_id
        before insert or update of install_id on public.installation_events
        for each row
        execute function public.canonicalize_install_id_reference();

        drop trigger if exists trg_installation_events_reconcile_install_id_migration on public.installation_events;
        create trigger trg_installation_events_reconcile_install_id_migration
        after insert on public.installation_events
        for each row
        execute function public.reconcile_install_id_migration_event();
    end if;

    if to_regclass('public.error_logs') is not null then
        drop trigger if exists trg_error_logs_canonicalize_install_id on public.error_logs;
        create trigger trg_error_logs_canonicalize_install_id
        before insert or update of install_id on public.error_logs
        for each row
        execute function public.canonicalize_install_id_reference();
    end if;

    if to_regclass('public.license_activations') is not null then
        drop trigger if exists trg_license_activations_canonicalize_install_id on public.license_activations;
        create trigger trg_license_activations_canonicalize_install_id
        before insert or update of install_id on public.license_activations
        for each row
        execute function public.canonicalize_install_id_reference();
    end if;
end $$;

do $$
declare
    v_event record;
    v_payload jsonb;
    v_previous_install_id text;
begin
    if to_regclass('public.installation_events') is null then
        return;
    end if;

    for v_event in
        select install_id, payload_json
        from public.installation_events
        where event_type = 'install_id_migrated'
        order by occurred_at, created_at
    loop
        begin
            v_payload := v_event.payload_json::jsonb;
        exception when others then
            continue;
        end;

        v_previous_install_id := nullif(btrim(v_payload ->> 'previous_install_id'), '');
        if v_previous_install_id is null or v_previous_install_id = v_event.install_id then
            continue;
        end if;

        perform public.reconcile_install_id_alias(
            v_previous_install_id,
            v_event.install_id,
            'backfill'
        );
    end loop;
end $$;
