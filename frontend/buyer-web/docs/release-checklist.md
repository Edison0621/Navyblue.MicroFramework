# Buyer Release Checklist

## Functional regression

- login, browse, add-to-cart, checkout, pay, order detail
- order cancellation and tracking query
- review submit and after-sale request submit
- profile save, invoice title add
- membership/assets/activity pages must read/write via gateway contracts

## Quality gate

- `npm run lint`
- `npm run test -- --run`
- `npm run build`
- `dotnet build ../../samples/UserService/UserService.csproj`
- `dotnet build ../../samples/GatewayService/GatewayService.csproj`
- `docker compose config --quiet`

## Performance & UX

- product list uses query-based filtering/sorting
- cart and checkout use clear loading/error states
- toast feedback for key actions

## Integration

- gateway base URL configured with `VITE_GATEWAY_BASE_URL`
- API gap checklist reviewed with backend
- `samples/GatewayService/BuyerManagement.http` smoke run passed for admin + buyer paths
- rollback script and RC notes reviewed in `docs/prd-closure-status.md`
