# Feature Specification: Close Register Menu

**Feature Branch**: `005-close-register-menu`  
**Created**: 2026-04-22  
**Status**: Draft  
**Input**: User description: "/speckit.git.feature and also after pressing close register must logout and turn admin button to user profile menu"

## Clarifications

### Session 2026-04-22

- Q: What money totals should the close-register dialog show? → A: Show real current-session Cash and Card totals; omit Customer Account.
- Q: After a successful close, what should the screen do next? → A: Full logout and return to the login screen.
- Q: Who can close the active register session? → A: Any signed-in user can close the single active session.
- Q: What should happen if the user cancels the close dialog? → A: Close the dialog, discard typed values, keep the user signed in, and keep the session open.
- Q: What should happen if the close action fails to save? → A: Show an error, keep the user signed in, keep the session open, and keep the dialog open for retry.
- Q: How should older databases behave after upgrading to this feature? → A: Upgrade the existing local database in place, add the new register session table without backfilling fake history, and treat older databases as having no active session until the first real register-open action.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - User Profile Menu (Priority: P1)

Cashiers and admins need a dedicated user profile menu accessible from the main shell. The former generic "admin button" will be replaced by a visual indicator of the current user (name and image/initials) that opens a dropdown-style menu containing user-specific actions.

**Why this priority**: It establishes the navigation hub for user-specific actions and replaces generic UI placeholders.

**Independent Test**: Can be tested by clicking the user profile button in the shell, which should open the new menu showing the current user's details and a "Close Register" button.

**Acceptance Scenarios**:

1. **Given** the user is logged in, **When** they look at the main navigation shell, **Then** they see their name and profile image/initials instead of a generic button.
2. **Given** the user clicks their profile image, **When** the menu opens, **Then** they see the "Close Register" button.

---

### User Story 2 - Close Register Modal (Priority: P2)

Signed-in users need a way to close the active register session, reconcile cash, and log out securely.

**Why this priority**: It completes the register session lifecycle started in the previous feature (opening control).

**Independent Test**: Can be tested by opening the Close Register modal, viewing the calculated differences, and clicking the close action.

**Acceptance Scenarios**:

1. **Given** the user clicks "Close Register" in the user menu, **When** the modal appears, **Then** they see the "Closing Register" summary containing expected cash, counted cash inputs, and calculated differences.
2. **Given** the user inputs their counted cash and notes, **When** they click "Close Register" in the modal, **Then** the register session is closed in the database and the system immediately logs the user out.
3. **Given** the modal is open, **When** they click "Cash In/Out", **Then** nothing happens (non-functional for now).
4. **Given** the modal is open, **When** they cancel or dismiss it, **Then** the dialog closes, any unsaved typed values are discarded, and the active session remains open.
5. **Given** the user confirms the close action, **When** the register close fails to save, **Then** the system shows an error, keeps the dialog open for retry, and leaves the active session open with the user still signed in.

### Edge Cases

- Given an existing local database created before register-session support, when the updated app starts, then the database is upgraded in place and no synthetic register session is created from historical sales data.
- Given an upgraded older database with zero register session rows, when a user reaches the checkout flow, then the system treats the store as having no active session until a real open-register action is recorded.
- Given a legacy database upgrade fails, when the app cannot add the new schema safely, then the system must not invent partial session state.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST replace the existing "admin button" with a user profile button displaying the current user's name and avatar/initials.
- **FR-002**: System MUST display a user menu popup when the profile button is clicked, featuring a "Close Register" action.
- **FR-003**: System MUST display a "Closing Register" modal when the action is triggered.
- **FR-004**: System MUST display expected totals for Cash and Card within the modal based on the current session.
- **FR-005**: System MUST allow the user to input the "Counted" cash amount and provide a closing note.
- **FR-006**: System MUST calculate and display the difference between Expected Cash and Counted Cash in real-time.
- **FR-007**: System MUST close the active register session in the database upon confirmation.
- **FR-008**: System MUST immediately log the user out and return to the login screen after successfully closing the register.
- **FR-009**: System MUST render the "Cash In/Out" button in the modal as non-functional (disabled or no-op) for this iteration.
- **FR-010**: System MUST NOT include a "Daily Sales" button in the modal.
- **FR-011**: System MUST allow any signed-in user to close the single active register session.
- **FR-012**: System MUST discard unsaved counted cash and note values when the close dialog is cancelled or dismissed.
- **FR-013**: System MUST show an error, keep the user signed in, keep the active session open, and keep the close dialog open for retry if the register close fails to save.
- **FR-014**: System MUST upgrade an existing local database in place by adding register-session schema objects without deleting or replacing prior business data.
- **FR-015**: System MUST NOT backfill or infer historical register sessions from pre-existing sales data when upgrading older databases.
- **FR-016**: System MUST treat an upgraded older database with no register session rows as having no active session until the first real register-open action is recorded.

### Key Entities

- **RegisterSession**: The active session entity that will be updated with closed timestamps and closing monetary amounts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can access the "Close Register" action in 1 click from the main shell.
- **SC-002**: System calculates cash differences with 100% mathematical accuracy.
- **SC-003**: System correctly logs the user out and returns to the login screen within 1 second of confirming the register closure.

## Assumptions

- The frontend design will follow WinUI 3 best practices with a bold, distinctive aesthetic as requested.
- Expected Cash and Card values are derived from the active register session at the time the close dialog opens.
- Customer Account is out of scope for this feature and MUST NOT be displayed in the close dialog.
- The user avatar can be initials if a profile picture URL is not available in the database.
- Existing merchant databases may already contain products, sales, receipts, and users but no register-session records before this feature is installed.
