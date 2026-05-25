namespace InterviewTranslator.Translate;

// Teknik terimlerin çeviri sırasında bozulmasını önler
public sealed class GlossaryService
{
    // Uzun terimler önce eşleşsin (örn. "pull request" önce, "request" sonra)
    private static readonly (string Term, string Canonical)[] TermList =
    [
        // --- Mimari & Sistem Tasarımı ---
        ("microservices architecture", "microservices architecture"),
        ("microservice",               "microservice"),
        ("monolithic architecture",    "monolithic architecture"),
        ("event-driven architecture",  "event-driven architecture"),
        ("domain-driven design",       "domain-driven design"),
        ("service mesh",               "service mesh"),
        ("api gateway",                "API gateway"),
        ("message queue",              "message queue"),
        ("message broker",             "message broker"),
        ("event sourcing",             "event sourcing"),
        ("cqrs",                       "CQRS"),
        ("circuit breaker",            "circuit breaker"),
        ("rate limiting",              "rate limiting"),
        ("load balancer",              "load balancer"),
        ("reverse proxy",              "reverse proxy"),
        ("cdn",                        "CDN"),
        ("dns",                        "DNS"),

        // --- DevOps & CI/CD ---
        ("ci/cd pipeline",             "CI/CD pipeline"),
        ("ci/cd",                      "CI/CD"),
        ("continuous integration",     "continuous integration"),
        ("continuous deployment",      "continuous deployment"),
        ("continuous delivery",        "continuous delivery"),
        ("infrastructure as code",     "Infrastructure as Code"),
        ("gitops",                     "GitOps"),
        ("blue-green deployment",      "blue-green deployment"),
        ("canary deployment",          "canary deployment"),
        ("rolling deployment",         "rolling deployment"),
        ("deployment",                 "deployment"),
        ("rollback",                   "rollback"),
        ("pipeline",                   "pipeline"),
        ("devops",                     "DevOps"),
        ("devsecops",                  "DevSecOps"),
        ("sre",                        "SRE"),
        ("on-call",                    "on-call"),
        ("postmortem",                 "postmortem"),
        ("runbook",                    "runbook"),

        // --- Konteyner & Orkestrasyon ---
        ("kubernetes",                 "Kubernetes"),
        ("docker compose",             "Docker Compose"),
        ("docker",                     "Docker"),
        ("container",                  "container"),
        ("pod",                        "pod"),
        ("helm chart",                 "Helm chart"),
        ("helm",                       "Helm"),
        ("namespace",                  "namespace"),
        ("cluster",                    "cluster"),
        ("node",                       "node"),
        ("ingress",                    "ingress"),
        ("sidecar",                    "sidecar"),

        // --- Bulut & Altyapı ---
        ("aws",                        "AWS"),
        ("azure",                      "Azure"),
        ("gcp",                        "GCP"),
        ("serverless",                 "serverless"),
        ("lambda",                     "Lambda"),
        ("s3",                         "S3"),
        ("ec2",                        "EC2"),
        ("vpc",                        "VPC"),
        ("iam",                        "IAM"),
        ("terraform",                  "Terraform"),
        ("ansible",                    "Ansible"),
        ("auto-scaling",               "auto-scaling"),
        ("auto scaling",               "auto-scaling"),

        // --- Yazılım Geliştirme ---
        ("pull request",               "pull request"),
        ("code review",                "code review"),
        ("refactoring",                "refactoring"),
        ("debugging",                  "debugging"),
        ("repository",                 "repository"),
        ("merge conflict",             "merge conflict"),
        ("branch",                     "branch"),
        ("commit",                     "commit"),
        ("rebase",                     "rebase"),
        ("cherry-pick",                "cherry-pick"),
        ("hot fix",                    "hotfix"),
        ("hotfix",                     "hotfix"),
        ("feature flag",               "feature flag"),
        ("technical debt",             "technical debt"),
        ("clean code",                 "clean code"),
        ("solid principles",           "SOLID principles"),
        ("design pattern",             "design pattern"),
        ("dependency injection",       "dependency injection"),
        ("unit test",                  "unit test"),
        ("integration test",           "integration test"),
        ("end-to-end test",            "end-to-end test"),
        ("tdd",                        "TDD"),
        ("bdd",                        "BDD"),
        ("mock",                       "mock"),
        ("stub",                       "stub"),

        // --- API & Protokoller ---
        ("rest api",                   "REST API"),
        ("graphql",                    "GraphQL"),
        ("grpc",                       "gRPC"),
        ("websocket",                  "WebSocket"),
        ("webhook",                    "webhook"),
        ("oauth",                      "OAuth"),
        ("jwt",                        "JWT"),
        ("openapi",                    "OpenAPI"),
        ("swagger",                    "Swagger"),
        ("rest",                       "REST"),
        ("api",                        "API"),

        // --- Veri & Depolama ---
        ("database",                   "database"),
        ("sql",                        "SQL"),
        ("nosql",                      "NoSQL"),
        ("postgresql",                 "PostgreSQL"),
        ("mongodb",                    "MongoDB"),
        ("redis",                      "Redis"),
        ("elasticsearch",              "Elasticsearch"),
        ("kafka",                      "Kafka"),
        ("rabbitmq",                   "RabbitMQ"),
        ("data pipeline",              "data pipeline"),
        ("etl",                        "ETL"),
        ("orm",                        "ORM"),
        ("migration",                  "migration"),
        ("sharding",                   "sharding"),
        ("replication",                "replication"),
        ("caching",                    "caching"),
        ("cache",                      "cache"),

        // --- Performans & Gözlemlenebilirlik ---
        ("throughput",                 "throughput"),
        ("latency",                    "latency"),
        ("p99",                        "P99"),
        ("p95",                        "P95"),
        ("sla",                        "SLA"),
        ("slo",                        "SLO"),
        ("sli",                        "SLI"),
        ("monitoring",                 "monitoring"),
        ("observability",              "observability"),
        ("tracing",                    "tracing"),
        ("logging",                    "logging"),
        ("metrics",                    "metrics"),
        ("dashboard",                  "dashboard"),
        ("alert",                      "alert"),
        ("prometheus",                 "Prometheus"),
        ("grafana",                    "Grafana"),
        ("opentelemetry",              "OpenTelemetry"),

        // --- Proje & Metodoloji ---
        ("agile",                      "Agile"),
        ("scrum",                      "Scrum"),
        ("kanban",                     "Kanban"),
        ("sprint",                     "sprint"),
        ("backlog",                    "backlog"),
        ("retrospective",              "retrospective"),
        ("standup",                    "standup"),
        ("story points",               "story points"),
        ("roadmap",                    "roadmap"),
        ("mvp",                        "MVP"),
        ("okr",                        "OKR"),
        ("kpi",                        "KPI"),
    ];

    private static readonly Dictionary<string, string> Terms =
        TermList.ToDictionary(x => x.Term, x => x.Canonical, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _placeholders = new();
    private int _counter;

    public string Protect(string text)
    {
        _placeholders.Clear();
        _counter = 0;

        // TermList sırasıyla işle — uzun terimler önce tanımlandığından doğru eşleşir
        foreach (var (term, canonical) in TermList)
        {
            if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                var placeholder = $"__TERM{_counter++}__";
                _placeholders[placeholder] = canonical;
                text = text.Replace(term, placeholder, StringComparison.OrdinalIgnoreCase);
            }
        }
        return text;
    }

    public string Restore(string text)
    {
        foreach (var (placeholder, term) in _placeholders)
            text = text.Replace(placeholder, term, StringComparison.OrdinalIgnoreCase);
        return text;
    }
}
