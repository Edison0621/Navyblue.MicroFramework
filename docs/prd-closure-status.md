# PRD Full Closure Status (Single Wave)

## Completed in this wave

- Contract convergence: unified response envelope / error codes / paging convention documented in `docs/buyer-management-contract.md`.
- User governance backend: batch level/tag/blacklist APIs added in `UserService` and exposed via `GatewayService`.
- Buyer domain backend: `UserService` buyer-domain APIs added for profile, loyalty, coupons, invoices, favorites, footprints, searches, and reviews.
- Buyer frontend closure: `frontend/buyer-web/src/lib/api.ts` switched core profile/assets/activity/review calls to gateway real APIs (no local fallback on those paths).
- Governance/platform frontend closure: platform user governance page supports batch operations and uses new gateway batch APIs.

## Risk & compliance coverage

- PII masking retained in platform governance list (`maskEmail`).
- Governance actions continue to produce audit events for tags/blacklist/level/close review.
- Close-account lifecycle remains enforced (`pending -> approved/rejected`) and synchronized with order guard constraints.

## Release gates executed

- `dotnet build samples/UserService/UserService.csproj` ✅
- `dotnet build samples/GatewayService/GatewayService.csproj` ✅
- `npm run lint` (`frontend/buyer-web`) ✅
- `npm run build` (`frontend/buyer-web`) ✅
- `npm test -- --run` (`frontend/buyer-web`) ✅
- `npm run lint` (`frontend/platform-admin`) ✅
- `npm run build` (`frontend/platform-admin`) ✅
- `docker compose config --quiet` ✅

## Notes

- This closure wave focuses on replacing critical local fallback paths with service-backed APIs and enabling production governance operations.
- Follow-up optional hardening: OpenAPI generation pipeline and E2E scripts per domain can be added as a dedicated release-engineering pass.
