"""This is as much a reliability fix as a security one because the database is the app’s operational root. fileciteturn6file0L1-L1 fileciteturn17file0L1-L1 fileciteturn30file0L1-L1

The fourth move is service-level authorization. Keep UI gating, but stop treating it as the ultimate boundary. Add an authorization service and enforce sensitive action checks in the service layer for user management, settings mutation, price override, future refunds/voids, and register control. That is the cleanest way to reduce privilege-bypass risk as the codebase grows. fileciteturn16file0L1-L1 fileciteturn25file0L1-L1 fileciteturn19file0L1-L1

The fifth move is operational resilience polish: standardize exception UX, health logging, and recovery paths. The code already captures and reports many exceptions; the next step is making failures user-recoverable where possible instead of presenting generic startup or page-level breakage. Telemetry’s outbox and retry model is a good internal example of a non-blocking strategy that should be copied into future integrations. fileciteturn17file0L1-L1 fileciteturn35file0L1-L1

**8. Priority Roadmap**

| Priority | Improvement | Security risk reduction | Business impact | Likelihood of exploitation/failure | Complexity | UX impact |
|---|---|---:|---:|---:|---:|---:|
| P0 | Remove default credential hint and force mandatory first-run credential change | Very High | Very High | Medium | Low | High positive |
| P0 | Add login throttling, lockout/backoff, and step-up auth for privileged actions | Very High | High | High | Medium | High positive |
| P0 | Increase password hash strength and phase out legacy SHA-256 | High | High | Medium | Medium | Neutral |
| P0 | Add startup DB backup/repair/rollback path | High | Very High | Medium | Medium | High positive |
| P1 | Add inactivity timeout / lock-on-idle | Medium | High | Medium | Medium | High positive |
| P1 | Add service-layer authorization checks | High | High | Medium | Medium | Medium |
| P1 | Improve local database protection and restore integrity workflow | High | High | Medium | Medium to High | Medium |
| P2 | Expand audit logs to denials, throttles, promotions, overrides, and admin settings mutations | Medium | Medium | Medium | Low | Medium |

**9. Final Expert Verdict**

The safest next steps are not broad and abstract; they are concrete and local to what the repository already shows. First, close the onboarding/default-credential weakness and add real throttling. Second, modernize credential protection and require step-up authentication for privileged actions. Third, treat the SQLite database and startup initialization chain as critical infrastructure with backup, repair, and rollback support. Fourth, harden authorization at the service layer so the application does not depend on UI gating alone. If those changes are made while preserving the existing optional/offline-safe telemetry approach and the current role-aware navigation model, the application can materially reduce unauthorized-access risk and lower the chance that one failed component takes down the business-critical path. fileciteturn28file0L1-L1 fileciteturn25file0L1-L1 fileciteturn19file0L1-L1 fileciteturn17file0L1-L1 fileciteturn35file0L1-L1 citeturn2search0turn2search5