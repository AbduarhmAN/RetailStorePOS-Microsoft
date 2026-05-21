-- ---------------------------------------------------------------------------
-- License key hash backfill — switch from SHA-256(key + pepper) to HMAC-SHA256
-- ---------------------------------------------------------------------------
-- The Edge Function changed its lookup algorithm in
--   supabase/functions/license-api/utils/crypto.ts (Task 3 hardening).
-- After the new function is deployed, every existing licenses.license_key_hash
-- row that was produced by the old algorithm will fail to match incoming
-- requests with `license_not_found`.
--
-- This template re-stamps each license_key_hash by re-hashing the original
-- license key value. The plaintext key is NOT stored in Supabase, so the only
-- way to backfill is to source the keys from your offline issuance ledger
-- (LicenseKeyGen output, encrypted spreadsheet, etc.) and run UPDATE statements
-- with the new HMAC values.
--
-- Steps:
--   1. Stop issuing new licenses.
--   2. Compute the new hash for each plaintext key. From the project root:
--
--        cd LicenseKeyGen
--        dotnet run -- --pepper "<LICENSE_KEY_PEPPER>" \
--                       --hmac \
--                       --keys-file ./issued-keys.txt
--
--      That command should print one line per key in the form:
--          RSPS-XXXX-XXXX-XXXX  <hex_hmac>
--
--   3. Convert the output into UPDATE statements similar to the example below.
--   4. Open a Supabase psql session as the service role and run them inside a
--      single transaction so a partial failure leaves the table consistent.
--   5. Re-deploy the Edge Function and run a smoke test against one known key.
--
-- Example (replace the placeholder with your real values):
-- ---------------------------------------------------------------------------
begin;

-- update public.licenses
--    set license_key_hash = '<new_hex_hmac_value>',
--        updated_at = timezone('utc'::text, now())
--  where license_key_prefix = 'RSPS-DEMO'
--    and license_key_hash   = '<old_hex_sha256_value>';

-- repeat one UPDATE per issued key …

commit;

-- After commit, verify with:
--   select license_key_prefix, length(license_key_hash) as hash_len, status
--     from public.licenses
--    order by created_at desc
--    limit 10;
--
-- All hashes should now be 64-character hex strings (HMAC-SHA256 output).
