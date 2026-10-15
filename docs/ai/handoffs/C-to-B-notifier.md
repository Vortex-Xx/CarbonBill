# Handoff: C-to-B-notifier

- **From**: Dev 3 (Insights and Output / Track C)
- **To**: Dev 2 (Capture and Extraction / Track B)
- **Date**: 2026-10-09
- **Topic**: Notifications Subsystem, Floor PWA Web Push (VAPID), and Document Retake Alerts

---

## 1. Overview of Notifications Subsystem (Deliverable C2)

Track C has completed Deliverable C2 (**Notifications Subsystem**). The module provides:
1. **Multi-Channel Dispatching**:
   - **In-App**: Notification feed stored in database (`Notifications` table) with read tracking and dual tenant isolation.
   - **Web Push**: VAPID Web Push protocol for browser service workers / Floor PWA.
   - **Email**: Brevo, Resend, or Logging provider chosen via configuration (`Notifications:Email:Provider`).
2. **Caller Resilience**:
   - `INotifier` wraps all outbound requests with Polly exponential backoff retries (3 attempts).
   - If a provider fails or network is down, the exception is caught, logged, and `false` is returned safely. **A notification provider failure will NEVER throw or break the caller**.
3. **Bilingual Formatting (`bn` / `en`)**:
   - Bangla-first by default.
   - Formats dates and numerals using Bangla digits (`০১২৩৪৫৬৭৮৯`).
   - All reports and weekly digests include the mandatory GHG Protocol methodology disclaimer.
4. **Flag Fatigue & Quiet Hours Controls**:
   - **Deduplication / Collapsing**: Repeated unread alerts collapse into a single item with an incremented `CollapseCount` instead of spamming users.
   - **Snooze with Reason**: Users can snooze alerts for 7, 14, or 30 days with a mandatory justification reason.
   - **Quiet Hours**: Respects user local time (default 22:00 to 08:00 Asia/Dhaka UTC+6). Non-critical alerts are suppressed during quiet hours, while critical `Red` alerts bypass quiet hours immediately.

---

## 2. How Track B Should Trigger Retake Alerts

When a reviewer rejects a document (blurry image, obscured meter, cut-off slip) or processing fails, Track B should publish domain events via `IDomainEventPublisher`:

### A. Document Retake Request (Floor PWA Nudge)
Publish `DocumentRetakeRequestedEvent`:
```csharp
await eventPublisher.PublishAsync(new DocumentRetakeRequestedEvent(
    DocumentId: document.Id,
    OrgId: document.OrgId,
    UploaderUserId: document.UploaderUserId, // Target floor staff member
    FileName: document.FileName,
    Reason: "Image is blurry; meter serial number cannot be recognized",
    OccurredOnUtc: DateTime.UtcNow
), cancellationToken);
```

**What happens automatically**:
1. Notifications module resolves `UploaderUserId`'s preferred language (`bn` or `en`).
2. Renders bilingual retake copy:
   - **BN**: `নথি পুনরায় ছবি তোলা প্রয়োজন: 'generator_diesel_slip.jpg' নথির স্পষ্ট ছবি পুনরায় আপলোড করুন। কারণ: {reason}`
   - **EN**: `Document Retake Required: Please capture and re-upload a clear copy of '{fileName}'. Reason: {reason}`
3. Dispatches Web Push immediately to the floor staff device with deep-link `/capture?docId={docId}`.
4. Stores in-app notification with deduplication key `RETAKE:{documentId}`.

### B. Unrecoverable Processing Failure
Publish `DocumentFailedEvent`:
```csharp
await eventPublisher.PublishAsync(new DocumentFailedEvent(
    DocumentId: document.Id,
    OrgId: document.OrgId,
    UploaderUserId: document.UploaderUserId,
    FileName: document.FileName,
    FailureReason: "File format damaged or unreadable",
    OccurredOnUtc: DateTime.UtcNow
), cancellationToken);
```

---

## 3. Floor PWA Web Push Subscription Endpoints

For the Floor PWA service worker (owned by Dev 2 in `web/src/features/capture` and `web/src/app`):

### Register Push Subscription
- **Endpoint**: `POST /api/v1/notifications/push-subscription`
- **Request Body**:
```json
{
  "endpoint": "https://fcm.googleapis.com/fcm/send/...",
  "p256dh": "BNcRdreALRFXTkOOUHK1EtK2wtaz5...",
  "auth": "tBHItJI5svbpez7KI4CCXg==",
  "userAgent": "Mozilla/5.0 (Linux; Android 14; Pixel 7)..."
}
```

### Unregister Push Subscription
- **Endpoint**: `DELETE /api/v1/notifications/push-subscription?endpoint={endpoint}`

### In-App Notifications Feed
- **Endpoint**: `GET /api/v1/notifications?unreadOnly=true&page=1&pageSize=20`
- **Mark As Read**: `POST /api/v1/notifications/{id}/read`
- **Mark All Read**: `POST /api/v1/notifications/read-all`
- **Snooze**: `POST /api/v1/notifications/snooze`
  ```json
  {
    "alertOrFlagKey": "FLAG:SPIKE-001",
    "days": 14,
    "reason": "Overtime production surge for Eid shipment"
  }
  ```
- **Preferences**: `GET /api/v1/notifications/preferences` and `PUT /api/v1/notifications/preferences`
