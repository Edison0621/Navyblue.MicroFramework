# Buyer Management Contract (Phase 1)

## Domain models

- `UserTag`: buyer segment labels (`new_user`, `vip`, `risk_watch`, ...)
- `UserBlacklistState`: blacklist flag, reason, optional expiration
- `UserLevel`: `level`, `points`, `growthValue`
- `UserCloseRequestState`: close request status (`pending`, `approved`, `rejected`) and review trail

## Permission matrix

- `buyer`
  - read/update own profile
  - manage own addresses
  - submit own account close request
- `admin`
  - query users
  - update user tags / blacklist / level
  - review close requests
- `super-admin`
  - all admin actions + emergency override

## UserService APIs

- `GET /api/users?page=&pageSize=&q=&status=&tag=&blacklisted=`
- `GET /api/users/me`
- `PATCH /api/users/{id}/tags`
- `PATCH /api/users/{id}/blacklist`
- `PATCH /api/users/{id}/level`
- `POST /api/users/me/close-request`
- `POST /api/users/{id}/close-review`

## Gateway APIs

- `GET /api/gw/users`
- `GET /api/gw/users/me`
- `PUT /api/gw/users/{id}`
- `PATCH /api/gw/users/{id}/tags`
- `PATCH /api/gw/users/{id}/blacklist`
- `PATCH /api/gw/users/{id}/level`
- `POST /api/gw/users/me/close-request`
- `POST /api/gw/users/{id}/close-review`
- `GET /api/gw/users/me/profile`
- `PUT /api/gw/users/me/profile`
- `GET /api/gw/users/me/loyalty`
- `GET/POST /api/gw/users/me/coupons`
- `GET/POST /api/gw/users/me/invoices`
- `GET/POST /api/gw/users/me/favorites`
- `GET/POST /api/gw/users/me/footprints`
- `GET/POST /api/gw/users/me/searches`
- `GET/POST /api/gw/users/me/reviews`

## Unified standards

- envelope: `ApiResponse<T> { success, data, error, traceId }`
- error codes: `not_found`, `invalid_request`, `unauthorized`, `conflict`, `internal_error`
- pagination: `PageQuery(page,pageSize)` + `PagedResult(items,page,pageSize,total)`
- audit fields: `actor`, `action`, `resource`, `result`, `traceId`, `timestamp`

## Audit actions

- `user.tags.updated`
- `user.blacklist.updated`
- `user.level.updated`
- `user.close.requested`
- `user.close.reviewed`
