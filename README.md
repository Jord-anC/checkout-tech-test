# Payment Gateway Technical Assessment

## API behavior

### Process a payment

`POST /api/Payments` accepts a JSON payment request containing:

- `cardNumber`: 14-19 numeric digits
- `expiryMonth`: 1-12
- `expiryYear`: a future year combination
- `currency`: `GBP`, `USD`, or `EUR`
- `amount`: a positive integer representing minor currency units
- `cvv`: 3-4 numeric digits

Invalid requests return `400 Bad Request` before the acquiring bank is called. The optional `Idempotency-Key` header accepts 1-64 characters:

- Reusing a key with the same request returns the original payment response and does not call the bank again.
- Reusing a key with different payment data returns `409 Conflict`.
- Concurrent reuse while processing returns `409 Conflict`.
- Failed or timed-out bank requests release the key reservation so a later retry can be attempted.

Valid submissions return either `Authorized` or `Declined` with the payment ID, last four card digits, expiry, currency, and amount. Upstream unavailability returns `502 Bad Gateway`; an uncanceled upstream timeout returns `504 Gateway Timeout`.

### Retrieve a payment

`GET /api/Payments/{id}` returns `GetPaymentResponse` for an existing payment and `404 Not Found` for an unknown ID.

## Design considerations

### Clean code and SOLID principles

- **Single Responsibility Principle:** `PaymentsController` is responsible for HTTP orchestration and API responses, `BankPaymentClient` owns acquiring-bank communication, `PaymentsRepository` owns payment and idempotency storage, and the request/response models represent their own API boundaries. `IValidatableObject` keeps request validation out of controller branches.
- **Open/Closed Principle:** `IBankPaymentClient` allows the bank implementation to change or additional acquiring banks to be added without changing controller behavior. Separate request, bank-wire, and merchant-response models also allow external contracts to evolve independently. The repository abstraction provides the same benefit for storage implementations.
- **Liskov Substitution Principle:** `PaymentsRepository` implements `IPaymentsRepository` and can be substituted by another repository implementation as long as it preserves payment lookup and idempotency semantics. Component tests demonstrate this by substituting test implementations and the bank client without changing controller code.
- **Interface Segregation Principle:** `IBankPaymentClient` exposes only bank authorization and `IPaymentsRepository` exposes only payment storage and idempotency operations. Neither controller-facing abstraction forces implementers to support unrelated capabilities.
- **Dependency Inversion Principle:** `PaymentsController` depends on `IBankPaymentClient` and `IPaymentsRepository` rather than concrete HTTP or storage implementations. ASP.NET Core dependency injection creates `BankPaymentClient` and `PaymentsRepository`, allowing test doubles and future database or bank implementations to be substituted.

### API design

- Resource routes use `/api/Payments` for creation and retrieval.
- Validation failures use `400`, upstream outages use `502`, upstream timeouts use `504`, missing payments use `404`, and idempotency conflicts use `409`.
- Field-level validation messages are explicit and non-sensitive.
- The public response contains only the last four card digits. Full PAN and CVV values are not stored or returned.
- JSON uses the normal ASP.NET camel-case conventions for merchant requests and responses. The bank wire model explicitly uses snake_case where the simulator requires it.

### Bank communication and failure handling

The bank URL and timeout are configuration values. The client uses a five-second HTTP timeout and makes at most two retries, for three attempts total:

- Retries HTTP `408`, `429`, `502`, `503`, and `504` responses.
- Retries transient network exceptions.
- Does not retry `400` or other permanent failures.
- Uses short exponential delays with jitter to avoid synchronized retries.
- Honors request cancellation and does not retry a caller-canceled request.

Retries are intentionally bounded. Payment endpoints should not blindly retry because a bank may have accepted a payment but its response was lost. Idempotency therefore reserves a key before processing and records the original response for exact reuse.

### Storage and idempotency

The repository is an in-memory singleton test double, as allowed by the assessment. It is process-local and suitable for this offline exercise, but it is not durable or shared across application instances.

Idempotency keys are hashed with the complete submitted payment payload and retained alongside the resulting payment response in memory. The fingerprint includes the full card number and CVV only as a local SHA-256 hash and is not logged or returned. The hash allows exact payload reuse/conflict detection without storing a second copy of sensitive request fields.

A production implementation would require a durable, shared store with atomic key reservation, a retention policy, and transaction support. It should also propagate a stable idempotency/reference key to the bank if the bank supports it.

### Assumptions and trade-offs

- Three currencies were chosen: GBP, USD, and EUR. These are three common currencies which fit well with the minor currency unit requirement. Other currencies like BHD would not work with this rule, so in Production this would require extra mapping or a domain service that could handle these edge cases.
- The bank's `authorized` Boolean determines the merchant-facing status. `Rejected` is not used for created payment records; invalid input is represented by an HTTP `400` validation response and no payment record.
- The bank authorization code is not included in merchant responses because it is outside the assessment response contract.
- The existing Docker-based Mountebank setup is not required for deterministic unit/component tests, which substitute the bank or HTTP handler. It remains useful for manual and contract-style verification.
- In-memory storage is simple and testable but has no persistence, replication, or restart recovery. It is also only local to the one instance of the application. Using an external database would add overhead, latency and more points of failure, but persists data and can be shared between multiple services or instances. For a production-scale application this would be implemented.
- Request validation uses `IValidatableObject` with DataAnnotations because the current rules are simple and the combined expiry rule is naturally a property of the request. `ApiController` can reject invalid requests automatically with `400 Bad Request` before the bank is called, the rules can be tested directly in `PaymentRequestValidationTests`, and the controller remains focused on payment orchestration under the Single Responsibility Principle. The trade-off is that this approach becomes harder to manage for complex rules and is not as reusable across other entry points; a dedicated validator, shared rule service, or domain service would be preferable if validation grows or needs to be shared.

## Security and future production readiness

Before production, add:

- Secrets/configuration management and TLS for bank communication.
- A logging policy that excludes PAN, CVV, authorization headers, and full request bodies.
- Metrics for bank latency/outcomes, gateway latency/statuses, retries, idempotency reuse/conflicts, and repository failures.
- Correlation/tracing identifiers across the merchant request and bank call.
- Readiness/liveness checks, external-store health, and bank dependency health.
- A durable database and distributed idempotency store.
- PCI-compliant handling and an appropriate payment vault/tokenization service if any card data is retained beyond request processing.
