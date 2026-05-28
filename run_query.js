const token = process.env.SUPABASE_ACCESS_TOKEN;
const query = `
INSERT INTO public.licenses (id, license_key_hash, license_key_prefix, product_code, permission_group, status, max_devices) 
VALUES ('11111111-1111-1111-1111-111111111111', '0000000000000000000000000000000000000000000000000000000000000000', 'RSPS-DEV', 'RETAILSTOREPOS', 'PREMIUM', 'active', 999) 
ON CONFLICT (license_key_hash) DO NOTHING;

INSERT INTO public.license_activations (license_id, install_id, status, last_request_sequence) 
VALUES ('11111111-1111-1111-1111-111111111111', 'E65E5823AF03AB17690EC87FF5C79899D3588B10DCDAB0103E393685858E7441', 'active', 1) 
ON CONFLICT DO NOTHING;
`;

fetch('https://api.supabase.com/v1/projects/avuzwbmiiavbuaxmnitp/query', {
  method: 'POST',
  headers: {
    'Authorization': 'Bearer ' + token,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({ query: query })
}).then(res => res.text()).then(console.log).catch(console.error);
