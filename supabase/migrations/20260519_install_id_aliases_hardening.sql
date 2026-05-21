alter table public.install_id_aliases enable row level security;

revoke all on table public.install_id_aliases from public;
revoke all on table public.install_id_aliases from anon;
revoke all on table public.install_id_aliases from authenticated;

alter function public.resolve_canonical_install_id(text) security definer;
alter function public.resolve_canonical_install_id(text) set search_path = public;

alter function public.reconcile_install_id_alias(text, text, text) security definer;
alter function public.reconcile_install_id_alias(text, text, text) set search_path = public;

alter function public.canonicalize_install_id_reference() security definer;
alter function public.canonicalize_install_id_reference() set search_path = public;

alter function public.reconcile_install_id_migration_event() security definer;
alter function public.reconcile_install_id_migration_event() set search_path = public;

revoke all on function public.resolve_canonical_install_id(text) from public;
revoke all on function public.resolve_canonical_install_id(text) from anon;
revoke all on function public.resolve_canonical_install_id(text) from authenticated;
grant execute on function public.resolve_canonical_install_id(text) to service_role;

revoke all on function public.reconcile_install_id_alias(text, text, text) from public;
revoke all on function public.reconcile_install_id_alias(text, text, text) from anon;
revoke all on function public.reconcile_install_id_alias(text, text, text) from authenticated;
grant execute on function public.reconcile_install_id_alias(text, text, text) to service_role;
