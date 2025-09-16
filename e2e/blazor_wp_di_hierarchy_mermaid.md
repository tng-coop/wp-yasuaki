# BlazorWP – Dependency Injection Hierarchy

Here are two focused diagrams showing the DI wiring for **Editing** and **Streaming** services separately.

---

## Editing Services
```mermaid
graph TD
  classDef scoped fill:#e3f2fd,stroke:#1e88e5,color:#0d47a1;
  classDef singleton fill:#e8f5e9,stroke:#43a047,color:#1b5e20;
  classDef component fill:#f3e5f5,stroke:#8e24aa,color:#4a148c;

  C["Blazor Components"]:::component

  IApi["IWordPressApiService"]:::scoped
  Api["WordPressApiService"]:::scoped
  HCWP["HttpClient (WP BaseAddress)"]:::scoped
  AMH["AuthMessageHandler"]:::scoped
  Flags["AppFlags"]:::singleton

  IEditor["IPostEditor"]:::scoped
  Editor["WordPressEditor"]:::scoped

  C --> IApi
  IApi --> Api
  Api --> HCWP
  Api --> Flags
  HCWP --> AMH

  C --> IEditor
  IEditor --> Editor
  Editor --> HCWP
```

---

## Streaming Services
```mermaid
graph TD
  classDef scoped fill:#e3f2fd,stroke:#1e88e5,color:#0d47a1;
  classDef singleton fill:#e8f5e9,stroke:#43a047,color:#1b5e20;
  classDef options fill:#fff8e1,stroke:#fbc02d,color:#e65100;
  classDef component fill:#f3e5f5,stroke:#8e24aa,color:#4a148c;

  C["Blazor Components"]:::component

  IApi["IWordPressApiService"]:::scoped
  Api["WordPressApiService"]:::scoped
  HCWP["HttpClient (WP BaseAddress)"]:::scoped

  IFeed["IPostFeed"]:::singleton
  Feed["PostFeed"]:::singleton
  IStream["IContentStream"]:::scoped
  Stream["ContentStream"]:::scoped
  Cache["IPostCache -> MemoryPostCache"]:::singleton
  SOpts["IOptions<StreamOptions>"]:::options

  C --> IFeed
  IFeed --> Feed
  Feed --> IStream
  Feed --> SOpts

  IStream --> Stream
  Stream --> HCWP
  Stream --> Cache

  Api --> HCWP
  C --> IApi
  IApi --> Api
```

---

### Notes
- Editing focuses on `IPostEditor` and `WordPressEditor` which rely on the WP-configured `HttpClient`.
- Streaming centers on `IContentStream` and `IPostFeed`, with caching via `MemoryPostCache` and configuration via `IOptions<StreamOptions>`.
- Both parts resolve their `HttpClient` through `IWordPressApiService`, ensuring consistent configuration and authentication.
