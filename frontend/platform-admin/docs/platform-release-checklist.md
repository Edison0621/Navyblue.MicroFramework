# Platform Admin Release Checklist

## Sprint 1 foundation
- shell layout / route guard / session bootstrap
- RBAC permission map and guarded module routes
- unified API wrapper and error handling
- frontend audit hook for critical operations

## Sprint 2 merchant + category
- merchant status actions (normal/frozen/closed)
- category tree read path and rendering

## Sprint 3 product + marketing
- product audit queue (approve/reject)
- marketing module entry and rule-check placeholders

## Sprint 4 order + user governance
- order arbitration module entry
- user governance (list/search/tags/blacklist/level/close-review)

## Sprint 5 finance + ticket + content
- finance settlement module entry
- ticket workflow module entry
- content management module entry

## Sprint 6 risk + system + release
- risk policy module entry
- system settings module entry
- lint/build pass and compose configuration valid

## Quality gate
- `npm run lint`
- `npm run build`
- `docker compose config --quiet`
