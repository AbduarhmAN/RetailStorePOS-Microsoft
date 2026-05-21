# Research Gap Analysis and Next-Stage Topics for RetailStorePOS-Microsoft

## Existing Research and Project Context Review

The prior research set is already broad and non-random. Based on the provided markdown reports, it already covers five substantial lanes: feature-activation and licensing architecture, UI/XAML audit and refactor direction, premium reporting opportunities, future feature roadmap planning, and advertising-channel research. In the selected GitHub repository, that is reinforced by formal product/spec work on lifecycle telemetry, modular-monolith boundaries, register opening control, close-register flows, product pictures, multi-language support, and a readiness/dashboard freshness initiative. In other words, the project is not short on ideas or conceptual direction; it is short on a smaller number of decision-grade research answers that unblock the next buildable layer. fileciteturn15file0 fileciteturn12file0 fileciteturn16file0 fileciteturn17file0 fileciteturn18file0 fileciteturn19file0 fileciteturn13file0

The current application appears to be a local-first WinUI 3 desktop POS built as a two-project solution around a local SQLite database, with a Windows-targeted .NET 10 app, MSIX packaging enabled in the main project, and parallel Inno Setup installer scripts for direct-download distribution. The runtime initializes repositories and services for products, sales, tax, settings, users/auth, audit logs, register sessions, product search, telemetry, and readiness checks; secrets are stored with Windows DPAPI; and the deployment/release surface already includes both MSIX and single-file installer lanes. fileciteturn11file0 fileciteturn35file0 fileciteturn25file0 fileciteturn27file0 fileciteturn34file0

The inspected bootstrap schema and repositories show a real POS backbone rather than a prototype shell. The schema includes products, sales, sale items, users, sessions, audit logs, tax authorities/rules/groups, tender types, bundle components, telemetry outbox, installation events, and register sessions. The sales repository already handles receipt numbering, sale items, inventory decrement on sale, dashboard metrics such as revenue, profit, discount amount, median invoice, sparklines, and top products. The product import service already supports CSV import, image-path handling, and product identity via `product_dna`, which means catalog ingestion and analytics are already meaningful application concerns, not hypothetical future work. fileciteturn21file0 fileciteturn29file0 fileciteturn28file0

Some important areas remain unclear and should be treated as **requires source confirmation**. The repo contains a multi-language spec and locale resource files, but the completion state of full localization is unclear. A readiness/dashboard freshness spec exists, but the readiness plan itself notes that the current git branch remains `006-product-pictures`, so merge/implementation status is not yet certain. `LoginRuntime` seeds potential Supabase credentials into secure storage, but the production role of any remote service is still unclear from the inspected files alone. Most importantly, the inspected bootstrap schema did **not** show customer, supplier, purchase-order, return, inventory-movement, or split-tender allocation tables, but that absence only applies to the files reviewed here and should still be treated as **requires source confirmation** for unreviewed branches or unpublished work. fileciteturn19file0 fileciteturn22file4 fileciteturn22file6 fileciteturn22file8 fileciteturn13file0 fileciteturn25file0 fileciteturn21file0

## Research Gap Analysis

**Business strategy.** The project already has strong feature ideation and technical exploration, but it does not yet have a decision-grade answer to a more commercial question: which product wedge should become the primary paid offering, and how should that shape the next stage of development? The policy file explicitly anticipates free, paid, or subscription-based features in the future, but no inspected repo artifact yet turns that into a concrete packaging strategy. fileciteturn33file0

**Market positioning.** Advertising-channel research appears to be covered, but channel selection is not the same thing as positioning. No competitor-proof feature matrix, review-site messaging framework, or target-vertical positioning artifact was visible in the inspected repo, and that matters because current buyer research shows that retailers are increasing software spend while still experiencing substantial purchase regret when evaluation and implementation fit are weak. citeturn2search0

**Monetization.** Premium reports and licensing/feature-verification architecture have already been explored conceptually, but the commercial layer that should connect them is still missing: edition boundaries, trial mechanics, entitlement triggers, price anchors, and upgrade paths. Without that, there is a real risk of doing monetization engineering before determining what buyers should actually pay for. fileciteturn33file0

**Backend and database.** This is the most important technical gap. The schema already has strong sales/product/tax/user/register foundations, but the inspected bootstrap does not include general-purpose inventory movements, customer records, supplier records, purchase orders, return/refund domain tables, or split-tender allocation tables. At the same time, sale creation already decrements stock directly, which is workable for the current app but too narrow for the next wave of roadmap items. fileciteturn21file0 fileciteturn29file0

**Security.** There is meaningful security work already: permission flags, audit logging, DPAPI secret storage, and more than one prior verification design. But there are also hard gaps that remain surprisingly immediate. `LoginRuntime` still provisions a default bootstrap admin using `admin` / `1234` and a `1234` PIN on first run, and `UserRepository` still carries a legacy SHA-256 verification fallback alongside PBKDF2. Broader app-wide authorization hardening, first-run security, recovery flows, and authorization test coverage are still missing as focused research answers. OWASP’s current guidance emphasizes least privilege, deny-by-default, logging, and explicit unit/integration tests for authorization logic, which maps directly onto this codebase. fileciteturn25file0 fileciteturn24file0 citeturn1search0turn1search8

**Reporting and analytics.** The product already has operational reporting and lifecycle telemetry, and the uploaded research already pushes further into premium reports. What is still missing is the bridge between those two: report-by-report data contracts, validation thresholds, and a clear answer for which premium metrics are actually supportable by the captured data today versus which need new tables or event streams first. The readiness plan also explicitly notes that there is no dedicated automated test project yet, which increases the value of research that turns reporting ideas into verifiable definitions. fileciteturn29file0 fileciteturn15file0 fileciteturn13file0

**Deployment.** The repo is currently signaling more than one release model at once: a packaged MSIX app in the project file, single-file Inno Setup installers in the `Deployment` folder, and a policy file that explicitly says code-signing or platform-verification status may vary by stage. Microsoft’s current Windows guidance makes packaging choice a roadmap-level decision rather than a cosmetic one, and SmartScreen reputation for direct downloads is now explicitly per file hash, which means distribution and signing strategy directly affect user trust on every new release. fileciteturn35file0 fileciteturn34file0 fileciteturn33file0 citeturn4search3turn3search0

**UI and UX.** One useful UI direction is already clear: the project does not need a wholesale redesign. The bigger gap now is user-journey research rather than more UI ideation: first-run setup, onboarding friction, cashier-speed benchmarks, empty-state handling, real-store accessibility acceptance criteria, and RTL or printed-receipt validation. The code/specs show localization work and many store workflows, but they do not yet answer whether those are optimized for first-day merchant success. **Requires source confirmation** on any existing unpublished UX studies beyond the inspected materials. fileciteturn19file0 fileciteturn22file6 fileciteturn22file8

**Customer adoption.** The app’s policy makes support intentionally limited and puts backup/device discipline on the merchant, while current retail software buying research shows that implementation difficulty and cybersecurity are among retailers’ leading concerns for 2025. That means acquisition research alone is not enough; the project also needs research on onboarding, migration, trial experience, support expectations, and what “time to first value” should actually look like. fileciteturn33file0 citeturn2search0

**Operational reliability.** Reliability research is present in the form of telemetry/lifecycle work and the readiness initiative, but it is still not complete as an operations discipline. The combination of local-first storage, merchant-responsible backups, new feature expansion, and no dedicated automated test project means the project still lacks decision-grade research on backup/restore drills, corruption recovery, large-dataset behavior, power-loss scenarios, and release-readiness gates. fileciteturn15file0 fileciteturn13file0 fileciteturn33file0

## Top 10 Highest-Value Research Topics

The priority order below is intentionally biased toward research that unlocks multiple downstream features at once, rather than another round of broad feature brainstorming.

**Priority 1 — Inventory movement ledger and stock adjustment architecture**  
**Research name:** Inventory movement ledger and stock adjustment architecture.  
**Exact search question:** *What offline-first SQLite data model, transaction boundaries, and module ownership rules should RetailStorePOS use for inventory movements, cycle counts, stock adjustments, transfers, receiving, returns, and sale decrements so that one auditable source of truth supports both future inventory workflows and premium reports?*  
**Why this research matters:** The inspected schema is rich in product and sales data, but sale completion still decrements stock directly and the reviewed bootstrap did not expose a general-purpose inventory movement ledger. That makes this the single strongest foundation gap behind cycle counting, replenishment, returns, transfer logic, and several premium inventory reports. fileciteturn21file0 fileciteturn29file0  
**What sources should be used:** `RetailStorePOS.Data/Class1.cs`, `SaleRepository.cs`, product modules, modular-monolith artifacts, the prior premium-report and future-feature research files, SQLite transaction and backup documentation, and inventory accounting references.  
**What decisions this research will unlock:** Whether to add `inventory_movements`; how stock is derived versus stored; which reasons/statuses are first-class; whether cost layers are needed now; and how many future features can share the same event model.  
**Expected final output:** A build-ready ERD, additive migration plan, transaction ownership map, invariants list, backfill strategy, and test cases for sale/return/adjustment/transfer flows.

**Priority 2 — Returns, exchanges, refunds, and tender reconciliation model**  
**Research name:** Returns, exchanges, refunds, and tender reconciliation model.  
**Exact search question:** *How should a local-first POS model return lines, exchange baskets, refund approvals, tax reversal, inventory restock, tender reversal, and split payments without crossing into prohibited payment-card storage?*  
**Why this research matters:** Sales currently persist a single `payment_type`, and the inspected schema did not reveal a tender-allocation or refund ledger. At the same time, the product policy explicitly says the app does not currently store cardholder data, and PCI guidance is clear that card verification codes cannot be retained after authorization. This topic is therefore both a domain-model problem and a compliance-boundary problem. fileciteturn29file0 fileciteturn21file0 fileciteturn33file0 citeturn1search7turn1search4  
**What sources should be used:** Sales and tax modules, register-session logic, the close-register spec and threat-model materials, PCI SSC FAQs, payment-processor integration docs, and retail accounting references for refunds and exchanges.  
**What decisions this research will unlock:** Refund state machine, approval policy, return linkage to original receipt, tender-allocation tables, future gift-card feasibility, and split-payment architecture.  
**Expected final output:** A return/refund state diagram, ledger schema, tax-reversal rules, approval matrix, failure/rollback cases, and a compliance boundary note for future payment integrations.

**Priority 3 — Supplier, purchase order, receiving, and cost-layer design**  
**Research name:** Supplier, purchase order, receiving, and cost-layer design.  
**Exact search question:** *What supplier, purchase-order, receiving, and inventory-cost model best fits a two-project WinUI + SQLite POS while preserving offline operation, partial receiving, and future replenishment/reporting needs?*  
**Why this research matters:** The inspected bootstrap did not show supplier or purchase-order tables, but the current schema already has dual-zone inventory, thresholds, product costs, and timestamps. That means the app has enough substrate to benefit from procurement research now, but not enough structure yet to implement it cleanly. fileciteturn21file0 fileciteturn29file0  
**What sources should be used:** Current schema/repositories, prior future-feature roadmap material, inventory and receiving workflows from retail operations references, and SQLite docs for transaction safety in partial receiving.  
**What decisions this research will unlock:** Whether procurement is modeled as headers/lines, whether receiving writes directly to inventory movements, how landed cost is treated, and whether weighted-average cost is enough for the next phase.  
**Expected final output:** Supplier/PO/receipt ERD, receiving state machine, cost-update rules, migration plan, and phased implementation scope.

**Priority 4 — Secure identity, first-run provisioning, and privilege hardening**  
**Research name:** Secure identity, first-run provisioning, and privilege hardening.  
**Exact search question:** *What concrete hardening plan should replace bootstrap default credentials, improve local auth recovery, enforce deny-by-default authorization, and reduce tamper or rollback risk while staying usable for offline retail operations?*  
**Why this research matters:** First-run setup still creates an administrator with `admin` / `1234` and a `1234` PIN, while user verification still carries a legacy SHA-256 fallback path. The app already has permissions, audit logs, secure secret storage, and prior verification architecture work, so the next missing answer is no longer “should there be security?” but “what exact hardening sequence should ship first?” OWASP’s authorization guidance fits this gap directly. fileciteturn25file0 fileciteturn24file0 fileciteturn27file0 citeturn1search0turn3search6  
**What sources should be used:** `LoginRuntime.cs`, `UserRepository.cs`, auth/audit modules, permission-bearing UI flows, the prior verification-design reports, OWASP authorization guidance, NIST ABAC references, and Windows auth/credential docs where relevant.  
**What decisions this research will unlock:** First-run secure setup flow, password/PIN policy, admin recovery method, session step-up boundaries, legacy-hash migration sequence, and authorization test coverage requirements.  
**Expected final output:** Threat model, prioritized remediation plan, auth lifecycle diagram, migration strategy, and explicit authorization test matrix.

**Priority 5 — Offline backup, restore, and sync-conflict strategy**  
**Research name:** Offline backup, restore, and sync-conflict strategy.  
**Exact search question:** *What backup, restore, snapshot, export, and optional sync architecture should a local-first SQLite POS use to recover from corruption, device loss, or multi-device scenarios without compromising data integrity or module boundaries?*  
**Why this research matters:** The policy file explicitly places local backup responsibility on the merchant, while the runtime uses a single local database path and the inspected repo did not show a first-class backup or sync domain. This becomes more important, not less, as the schema grows into procurement, returns, and premium reporting. fileciteturn25file0 fileciteturn33file0  
**What sources should be used:** Database path and repository patterns in the repo, readiness/report-store patterns, SQLite backup/WAL documentation, Windows file-system and installer guidance, and existing future-feature notes on backup/sync.  
**What decisions this research will unlock:** Backup UX shape, restore validation process, RPO/RTO targets, export/import boundary, and whether sync readiness is architected now or deferred.  
**Expected final output:** Architecture decision memo, backup file format and cadence, restore checklist, corruption-recovery drills, and sync-readiness options with trade-offs.

**Priority 6 — Windows distribution, code signing, update, and trust strategy**  
**Research name:** Windows distribution, code signing, update, and trust strategy.  
**Exact search question:** *What should be the single official Windows release strategy for RetailStorePOS: Microsoft Store, packaged MSIX direct distribution, MSIX with external location, or Inno/direct-download installs—and how should code signing, SmartScreen expectations, versioning, and update delivery work for each lane?*  
**Why this research matters:** The repo currently spans both MSIX packaging and Inno Setup installers, and the policy file explicitly says the code-signing/platform-verification status may vary by stage. Microsoft’s current guidance makes packaging a strategic product choice, and its SmartScreen documentation makes clear that reputation is per file hash for direct downloads, so each new build can reintroduce trust friction if the release path is not carefully chosen. fileciteturn35file0 fileciteturn34file0 fileciteturn33file0 citeturn4search3turn3search0turn0search5  
**What sources should be used:** `Nexill.RetailStorePOS.csproj`, `Deployment/*.iss`, policy text, Microsoft Learn documentation on packaging, MSIX, signing, App Installer, SmartScreen, and enterprise deployment.  
**What decisions this research will unlock:** Whether there is one release lane or multiple; which signing service to use; how updates work; whether package identity is required for future roadmap items; and how to explain trust prompts to pilot users.  
**Expected final output:** Release-lane decision memo, signing/update playbook, CI/CD requirements, user-facing install/update guidance, and a release checklist.

**Priority 7 — Pricing, packaging, trials, and entitlement design**  
**Research name:** Pricing, packaging, trials, and entitlement design.  
**Exact search question:** *Which commercial packaging should RetailStorePOS lead with—core POS, premium reports, advanced inventory, or security/activation—and how should free tier, trial limits, upgrade triggers, and feature entitlements align with the current codebase and buyer expectations?*  
**Why this research matters:** The policy text anticipates future paid or subscription-based features, the project has already explored premium reports and feature-activation architecture, and current retail buyer research shows growing software spend but also high regret when software fit and implementation are weak. This topic converts technical possibility into a revenue model that can actually shape build priorities. fileciteturn33file0 citeturn2search0  
**What sources should be used:** The provided premium-report, future-features, advertising-channel, and verification research files; selected repo maturity; competitor pricing/edition pages; review sites; and buyer research on software adoption and regret.  
**What decisions this research will unlock:** Which features belong in free versus paid plans, trial design, upsell surfaces, entitlement rules, and which roadmap items should be commercially first-class.  
**Expected final output:** Package matrix, value ladder, edition definitions, entitlement map, trial design spec, and pricing/positioning hypotheses.

**Priority 8 — Premium reports data contracts and metric validation**  
**Research name:** Premium reports data contracts and metric validation.  
**Exact search question:** *For each premium report already ideated, what exact tables, events, metric definitions, validation thresholds, and backtests are required before the report is safe to ship?*  
**Why this research matters:** Current reporting is already real and useful, but more advanced reporting is only as good as the event capture and validation behind it. The current code computes dashboard metrics from existing sales tables, while the readiness plan notes there is no dedicated automated test project yet. That combination makes report-validation research more valuable than adding another list of report ideas. fileciteturn29file0 fileciteturn13file0 fileciteturn21file0  
**What sources should be used:** `SaleRepository.cs`, current reporting UI/view-model flows, bootstrap schema, the prior premium-report research file, and external statistical/forecast-validation references where needed.  
**What decisions this research will unlock:** Which premium reports can ship immediately, which need new schema or events, what warnings or uncertainty labels are needed, and how forecast/exception quality is measured.  
**Expected final output:** Report-readiness matrix, metric dictionary, backtesting plan, data-quality guardrails, and implementation backlog.

**Priority 9 — Performance and scale benchmarks for real-store data volumes**  
**Research name:** Performance and scale benchmarks for real-store data volumes.  
**Exact search question:** *What are acceptable latency, memory, and responsiveness thresholds for checkout search, product import, dashboard load, and sales-history queries on representative low-end Windows hardware, and what concrete code/design changes are needed to hit them?*  
**Why this research matters:** The readiness plan already sets a five-second target for dashboard freshness under representative datasets, the import service is explicitly batched for scale, and the sales repository contains chunking and compatibility logic that show performance pressure is already being managed in code. That is a strong signal that performance is now mature enough to benchmark empirically rather than discuss abstractly. fileciteturn13file0 fileciteturn28file0 fileciteturn29file0  
**What sources should be used:** Current checkout/products/reporting code paths, the UI audit report, Microsoft WinUI performance guidance, import and query-heavy repository methods, and representative synthetic or pilot datasets.  
**What decisions this research will unlock:** Debounce rules, virtualization expectations, dashboard staging strategy, acceptable SKU/catalog limits, and whether certain reports must become asynchronous or paged.  
**Expected final output:** Benchmark harness design, dataset profiles, SLO/SLA targets, hotspot list, and a remediation backlog.

**Priority 10 — Merchant onboarding, migration, and time-to-value research**  
**Research name:** Merchant onboarding, migration, and time-to-value research.  
**Exact search question:** *What first-run setup, import templates, sample data, migration aids, help content, and support handoff are required so a retailer can reach first sale and first useful report quickly and safely?*  
**Why this research matters:** Retailers report implementation and cybersecurity as leading concerns when buying software, and many regret software purchases when pre-purchase and implementation fit are weak. The current app already has real configuration complexity across users, taxes, register rules, imports, and deployment, while the policy file makes support intentionally limited. That makes adoption-friction research a direct product-development topic, not just a marketing concern. fileciteturn28file0 fileciteturn33file0 citeturn2search0  
**What sources should be used:** Current login/setup/settings/tax/register/import flows, the UI audit report, support/policy text, future-feature priorities, and buyer/onboarding research from software review or implementation sources.  
**What decisions this research will unlock:** Setup wizard scope, sample-store mode, migration documentation, help center priorities, and what “time to first value” should mean operationally.  
**Expected final output:** Onboarding journey map, friction log, migration playbook, setup checklist, content backlog, and success metrics.

## Additional Optional Research Topics

These are worthwhile, but they are slightly less immediate than the top 10 because they either depend on earlier foundational answers or need target-market confirmation first.

| Optional topic | Why it could add value | When it becomes urgent |
|---|---|---|
| Competitive feature matrix and review-site proof strategy | Converts channel research into sharper positioning, objection handling, and review-site messaging | After pricing/packaging research begins |
| Target-geography tax, invoicing, privacy, and labor compliance **requires source confirmation** | Prevents building workflows that later fail local legal requirements | As soon as target launch countries or states are fixed |
| Customer accounts, loyalty, digital receipts, and identity domain | Supports retention and personalized offers, but depends on customer-data boundaries and onboarding strategy | After returns/tender and onboarding research |
| Promotion and price execution engine | Valuable, but depends on inventory/event quality and possibly customer segmentation | After inventory ledger and report data-contract work |
| Multi-store and franchise topology | Important for scale, but premature unless multi-entity operations are confirmed | When multi-location selling is a real product goal |
| Accessibility acceptance testing and RTL/receipt QA | Especially valuable if Arabic or other RTL deployment is imminent | Before any production pilot in multilingual markets |

## Research Priority Matrix

Scores are relative judgments based on the inspected repo, prior research coverage, and the degree to which each topic unlocks near-term implementation. Scores are on a 1–5 scale, where **5 is highest** and, for **difficulty**, **5 is hardest**.

| Rank | Research topic | Business impact | Technical impact | Urgency | Difficulty | Risk reduction | Usefulness for next development phase |
|---|---|---:|---:|---:|---:|---:|---:|
| 1 | Inventory movement ledger and stock adjustment architecture | 5 | 5 | 5 | 4 | 5 | 5 |
| 2 | Returns, exchanges, refunds, and tender reconciliation model | 5 | 5 | 5 | 4 | 5 | 5 |
| 3 | Supplier, purchase order, receiving, and cost-layer design | 5 | 4 | 4 | 4 | 4 | 5 |
| 4 | Secure identity, first-run provisioning, and privilege hardening | 5 | 4 | 5 | 3 | 5 | 5 |
| 5 | Offline backup, restore, and sync-conflict strategy | 4 | 5 | 5 | 4 | 5 | 5 |
| 6 | Windows distribution, code signing, update, and trust strategy | 4 | 4 | 4 | 3 | 4 | 4 |
| 7 | Pricing, packaging, trials, and entitlement design | 5 | 3 | 4 | 3 | 4 | 4 |
| 8 | Premium reports data contracts and metric validation | 4 | 4 | 4 | 3 | 4 | 4 |
| 9 | Performance and scale benchmarks for real-store data volumes | 4 | 4 | 4 | 3 | 4 | 4 |
| 10 | Merchant onboarding, migration, and time-to-value research | 4 | 3 | 4 | 2 | 3 | 4 |

## Recommended Research Execution Order

**First:** inventory movement ledger and stock adjustment architecture. This has the largest downstream unlock because it sits underneath cycle counts, receiving, returns, transfers, replenishment, and premium inventory reporting. It is the best use of the very next research slot. fileciteturn21file0 fileciteturn29file0

**Second:** returns, exchanges, refunds, and tender reconciliation. This settles the money side and the stock-reversal side at the same time, while respecting the current payment-data boundary. It also prevents later rework in gift cards, split tender, approvals, and exchange flows. fileciteturn29file0 fileciteturn33file0 citeturn1search7

**Third:** secure identity, first-run provisioning, and privilege hardening. The default bootstrap-admin path is an avoidable and immediate risk. This topic should move ahead of most growth features because every admin- or premium-gated feature depends on trustworthy access control. fileciteturn25file0 fileciteturn24file0 citeturn1search0

**Fourth:** supplier, purchase order, receiving, and cost-layer design. Once inventory events and money reversal logic are framed properly, procurement is the next clean extension of the domain model and a direct precursor to replenishment work. fileciteturn21file0

**Fifth:** offline backup, restore, and sync-conflict strategy. This becomes more urgent once the core write paths start expanding. It is better to define recovery and restore guarantees before new financial and inventory tables multiply operational risk. fileciteturn25file0 fileciteturn33file0

**Sixth:** Windows distribution, code signing, update, and trust strategy. This should be in place before broader piloting or external release because packaging, SmartScreen, and update behavior directly shape install trust and support burden. fileciteturn35file0 fileciteturn34file0 citeturn4search3turn3search0

**Seventh:** pricing, packaging, trials, and entitlement design. Once the foundational product wedge is clearer, this research can translate technical direction into a commercial structure instead of guessing too early. fileciteturn33file0 citeturn2search0

**Eighth:** premium reports data contracts and metric validation. This belongs after the first foundation topics because report feasibility depends heavily on data-model choices made in earlier topics. fileciteturn29file0 fileciteturn21file0

**Ninth:** performance and scale benchmarks. It is most valuable after the key domain decisions are made, so benchmark targets reflect the real architecture rather than an intermediate state. fileciteturn13file0 fileciteturn28file0

**Tenth:** merchant onboarding, migration, and time-to-value. This should be designed against the real setup flow, security model, install path, and import behavior that the earlier topics define. fileciteturn28file0 fileciteturn33file0 citeturn2search0

## Final Expert Recommendation

The next stage of research should **not** be another broad roadmap brainstorm, another generic UI rethink, or another channel-discovery exercise. Those areas are already covered. The project already knows many plausible things it *could* build. What it does **not** yet know with enough precision is what data model, trust model, recovery model, release model, and commercial model should sit underneath the next build phase. fileciteturn12file0 fileciteturn13file0 fileciteturn21file0 fileciteturn25file0 fileciteturn33file0

If only one research slot were used immediately, it should go to **inventory movement ledger and stock adjustment architecture**. If the goal is to use the remaining slots for maximum practical value, the strongest cluster is this: **inventory movement ledger**, **returns/tender reconciliation**, **secure identity hardening**, **supplier/PO/receiving**, and **backup/restore/sync**. That cluster attacks the deepest constraints visible in the current codebase: direct stock decrement without a general movement ledger, single-field payment typing without tender allocation, insecure default first-run credentials, missing procurement domain structures, and a local-first product that still leaves backup discipline largely to the merchant. fileciteturn29file0 fileciteturn21file0 fileciteturn25file0 fileciteturn33file0

The clearest strategic direction is therefore this: **move from idea research to foundation research**. The product is ready to stop asking “what feature should come next?” and start asking “what underlying structure must exist so the next five features do not create rework?” That is the research shift most likely to improve the application technically, commercially, and strategically.