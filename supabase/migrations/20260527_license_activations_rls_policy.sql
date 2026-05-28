-- ---------------------------------------------------------------------------
-- Add SELECT policy on license_activations for anonymous/authenticated roles
-- This allows the client-side direct REST DB check to succeed for verification.
-- ---------------------------------------------------------------------------

create policy "Allow select on license_activations for install verification"
on public.license_activations
for select
to anon, authenticated
using (true);
