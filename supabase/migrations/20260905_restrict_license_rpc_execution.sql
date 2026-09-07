begin;

-- These SECURITY DEFINER routines are backend implementation details. The
-- default PUBLIC execute privilege would let API roles bypass RLS and mutate
-- licensing state directly.
revoke all on function public.claim_license_seat(
    uuid,
    text,
    integer,
    text,
    bigint,
    timestamp with time zone
) from public, anon, authenticated;

revoke all on function public.refresh_license_activation(
    uuid,
    text,
    bigint,
    timestamp with time zone
) from public, anon, authenticated;

revoke all on function public.resolve_canonical_install_id(text)
    from public, anon, authenticated;

grant execute on function public.claim_license_seat(
    uuid,
    text,
    integer,
    text,
    bigint,
    timestamp with time zone
) to service_role;

grant execute on function public.refresh_license_activation(
    uuid,
    text,
    bigint,
    timestamp with time zone
) to service_role;

grant execute on function public.resolve_canonical_install_id(text)
    to service_role;

commit;
