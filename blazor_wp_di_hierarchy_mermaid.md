# BlazorWP – Dependency Injection Hierarchy

Here are two focused diagrams showing the DI wiring for **Editing** and **Streaming** services separately.

---

## Editing Services
```mermaid
graph TD
  %% Styles
  classDef scoped fill:#e3f2fd,stroke:#1e88e5,color:#0d47a1;
  classDef singleton fill:#e8f5e9,stroke:#43a047,color:#1b5e20;
  classDef component fill:#f3e5f5,stroke:#8e24aa,color:#4a148c;
  classDef di fill:#fff8e1,stroke:#fbc02d,color:#e65100;

  C["Blazor Components"]:::component

  HCWP["HttpClient (WP BaseAddress)"]:::scoped

  IApi["IWordPressApiService"]:::singleton
  Api["WordPressApiService"]:::singleton

  IEditor["IPostEditor"]:::scoped
  Editor["WordPressEditor"]:::scoped

  WPDI["WPDI.AddWordPressEditing()"]:::di

  %% Consumers
  C --> IApi
  C --> IEditor

  %% API chain
  IApi --> Api
  Api --> HCWP

  %% Editor depends on IWordPressApiService
  IEditor --> Editor
  Editor --> IApi

  %% Registration
  WPDI --> IEditor

```

---

## Streaming Services
```mermaid
graph TD
  classDef scoped fill:#e3f2fd,stroke:#1e88e5,color:#0d47a1;
  classDef singleton fill:#e8f5e9,stroke:#43a047,color:#1b5e20;
  classDef options fill:#fff8e1,stroke:#fbc02d,color:#e65100;
  classDef component fill:#f3e5f5,stroke:#8e24aa,color:#4a148c;
  classDef di fill:#fff8e1,stroke:#fbc02d,color:#e65100;

  C["Blazor Components"]:::component

  IApi["IWordPressApiService"]:::singleton
  Api["WordPressApiService"]:::singleton
  HCWP["HttpClient (WP BaseAddress)"]:::scoped

  IFeed["IPostFeed"]:::singleton
  Feed["PostFeed"]:::singleton

  IStream["IContentStream"]:::scoped
  Stream["ContentStream"]:::scoped

  Cache["IPostCache → MemoryPostCache"]:::singleton
  SOpts["IOptions<StreamOptions>"]:::options

  WPDI["WPDI.AddWpdiStreaming()"]:::di

  %% Consumers
  C --> IFeed
  C --> IApi

  %% Feed wiring
  IFeed --> Feed
  Feed --> IStream
  Feed --> SOpts

  IStream --> Stream
  Stream --> IApi
  Stream --> Cache

  IApi --> Api
  Api --> HCWP

  %% Registration
  WPDI --> IFeed
  WPDI --> IStream
  WPDI --> SOpts

```

---

### Notes
- **Editing**: centers on `IPostEditor` → `WordPressEditor` (**Scoped**), which is registered via `AddWordPressEditing`. That method wires `WordPressEditor` to consume the singleton `IWordPressApiService`:contentReference[oaicite:0]{index=0}. The service encapsulates the WP-configured `HttpClient` and auth options.
- **Streaming**: pivots on `IPostFeed` (**Singleton**) and `IContentStream` (**Scoped**), registered by `AddWpdiStreaming`. It also injects `IOptions<StreamOptions>` for configuration and an `IPostCache` (→ `MemoryPostCache`, **Singleton**) for caching:contentReference[oaicite:1]{index=1}.
- **WPDI role**: WPDI is the central extension layer that exposes `AddWordPressEditing` and `AddWpdiStreaming`. These ensure consumers only depend on abstractions (`IPostEditor`, `IPostFeed`, `IContentStream`) while the actual implementations (`WordPressEditor`, `PostFeed`, `ContentStream`) are consistently wired against `IWordPressApiService`. This provides a clean, testable DI surface, hides construction details, and enforces consistent HTTP/auth configuration:contentReference[oaicite:2]{index=2}.
- **Consistency**: both Editing and Streaming resolve all WordPress API calls through `IWordPressApiService`, ensuring a single source of truth for base URL, authentication, and lifetime.
