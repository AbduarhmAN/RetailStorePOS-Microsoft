-- 1. Create a manually forced Premium license for your testing purposes.
-- We use a dummy hash since you won't be verifying this specific key through the Edge Function anyway.
INSERT INTO public.licenses (
  id, 
  license_key_hash, 
  license_key_prefix, 
  product_code, 
  permission_group, 
  status, 
  max_devices
) VALUES (
  '11111111-1111-1111-1111-111111111111', 
  '0000000000000000000000000000000000000000000000000000000000000000',
  'RSPS-DEV',
  'RETAILSTOREPOS',
  'PREMIUM',
  'active',
  999
) ON CONFLICT (license_key_hash) DO NOTHING;

-- 2. Force an active seat for your exact Install ID linked to the dummy license.
INSERT INTO public.license_activations (
  license_id, 
  install_id, 
  status, 
  last_request_sequence
) VALUES (
  '11111111-1111-1111-1111-111111111111',
  'E65E5823AF03AB17690EC87FF5C79899D3588B10DCDAB0103E393685858E7441',
  'active',
  1
) ON CONFLICT DO NOTHING;
