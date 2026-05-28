-- ---------------------------------------------------------------------------
-- Fix refresh_license_activation() ambiguity introduced by the original
-- hardening migration. The RETURNS TABLE column name `last_request_sequence`
-- shadows the table column inside PL/pgSQL, so the UPDATE must qualify the
-- target table references explicitly.
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

grant execute on function public.refresh_license_activation(uuid, text, bigint, timestamptz) to service_role;
