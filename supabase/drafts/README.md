# Licensing protocol drafts

Files in this directory are design work, not deployable migrations.

`20260906_license_device_proof.sql` is the original incomplete device-proof
draft. It has not been deployed or verified against the live schema. Keep it
outside `supabase/migrations` until the client protocol, Edge Function,
enrollment authority, recovery, transaction locking, downgrade protection and
concurrency behavior have been reviewed together.

The existing v1 functions must remain available to unenrolled installations.
An enrolled installation must not bypass device proof through a v1 route.
Do not promote this draft merely because its SQL parses.
