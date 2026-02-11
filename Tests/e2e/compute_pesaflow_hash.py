import urllib.parse
import hmac
import hashlib
import base64

# Values taken from pesaflow_last_post_payload.json (last iframe POST)
API_KEY = "hkW0lc/+xu9GA5Di"
API_SECRET = "tgia2h6QEcwqPmJ1Uxv3V9I7cqf6Ub7X"
API_CLIENT_ID = "588"
SERVICE_ID = "235330"

payload = {
    "apiClientID": API_CLIENT_ID,
    "serviceID": SERVICE_ID,
    "billDesc": "Test Overload Fine",
    "currency": "KES",
    "billRefNumber": "LOCAL-20260211135546",
    "clientMSISDN": "254700000000",
    "clientName": "Test User",
    "clientIDNumber": "TEST-ID-001",
    "clientEmail": "test@truload-e2e.co.ke",
    "amountExpected": "100",
    "callBackURLONSuccess": "http://localhost:4000/api/v1/payments/callback/ecitizen-pesaflow",
    "notificationURL": "http://localhost:4000/api/v1/payments/webhook/ecitizen-pesaflow",
    "format": "json",
    "sendSTK": "false",
}

# Reconstruct raw form-encoded body (the order of keys is preserved as insertion order)
raw_body = urllib.parse.urlencode(payload)

# Compose data string per spec: apiClientID + amount + serviceID + clientIDNumber + currency + billRefNumber + billDesc + clientName + secret
amount_val = payload.get('amountExpected') or payload.get('amount')
data_string = f"{API_CLIENT_ID}{amount_val}{SERVICE_ID}{payload['clientIDNumber']}{payload['currency']}{payload['billRefNumber']}{payload['billDesc']}{payload['clientName']}{API_SECRET}"

# Compute HMAC-SHA256 using API_KEY as key
h = hmac.new(API_KEY.encode('utf-8'), data_string.encode('utf-8'), hashlib.sha256).digest()
hex_hash = h.hex()
# Final secureHash is base64 of lowercase-hex string bytes
secure_hash_final = base64.b64encode(hex_hash.encode('utf-8')).decode('utf-8')

print('\n--- Raw form-encoded body (exact bytes) ---')
print(raw_body)
print('\n--- data_string used for HMAC ---')
print(data_string)
print('\n--- HMAC-SHA256 (hex, lowercase) ---')
print(hex_hash)
print('\n--- secureHash (Base64 of hex string) ---')
print(secure_hash_final)

# Also print the secureHash recorded in last_post_payload.json for comparison
print('\n--- Recorded secureHash (from last_post_payload.json) ---')
print('NjZkYzY1ODc3NjQzZmM2NDQyNzlhMjg4YjEzMTM1OTNkNjY3YjU1NTE0NzNmNTRkODA0Y2U5NTgyYTAxODczZQ==')
