using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace InterviewTranslator.Desktop;

public partial class AnswerCardsWindow : Window
{
    private readonly GitHubDocsService _githubDocs;
    private List<AnswerCard> _allCards;
    private readonly ObservableCollection<AnswerCard> _visible = new();
    private string _activeCategory = "all";

    public AnswerCardsWindow(GitHubDocsService githubDocs)
    {
        _githubDocs = githubDocs;
        _allCards   = [.. _githubDocs.GetCards()];
        InitializeComponent();
        CardList.ItemsSource = _visible;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Ekranın sağ üst köşesine konumlan (overlay ile çakışmaz)
        Left = SystemParameters.PrimaryScreenWidth - Width - 20;
        Top  = 40;
        StatusLabel.Text = _githubDocs.StatusText;
        Refresh();
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        RefreshBtn.IsEnabled = false;
        RefreshBtn.Content   = "↻ Yükleniyor…";
        try
        {
            var cards = await _githubDocs.RefreshAsync();
            _allCards = [.. cards];
            StatusLabel.Text = _githubDocs.StatusText;
            Refresh();
        }
        finally
        {
            RefreshBtn.Content   = "↻ GitHub'dan Yenile";
            RefreshBtn.IsEnabled = true;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void CategoryTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryTabs.SelectedItem is TabItem tab)
            _activeCategory = tab.Tag?.ToString() ?? "all";
        Refresh();
    }

    private void Refresh()
    {
        var query = SearchBox.Text.Trim().ToLowerInvariant();
        var filtered = _allCards
            .Where(c => _activeCategory == "all" || c.Category == _activeCategory)
            .Where(c => string.IsNullOrEmpty(query)
                     || c.Topic.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || c.Answer.Contains(query, StringComparison.OrdinalIgnoreCase));

        _visible.Clear();
        foreach (var card in filtered)
            _visible.Add(card);
    }

    private void CardList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CardList.SelectedItem is AnswerCard card)
            PreviewBox.Text = card.Answer;
    }

    private void CardList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        CopySelected();
    }

    private void CopyBtn_Click(object sender, RoutedEventArgs e) => CopySelected();

    private void CopySelected()
    {
        if (CardList.SelectedItem is AnswerCard card)
            System.Windows.Clipboard.SetText(card.Answer);
    }
}

public sealed class AnswerCard
{
    public required string Category { get; init; }
    public required string Topic    { get; init; }
    public required string Answer   { get; init; }
}

public static class AnswerCardRepository
{
    public static readonly List<AnswerCard> All =
    [
        // ── TEKNİK ──────────────────────────────────────────────────────────
        new() { Category="technical", Topic="Microservice nedir?",
            Answer="A microservice is an independently deployable service that focuses on a single business capability. Services communicate via APIs or message queues and can be deployed, scaled, and updated independently." },

        new() { Category="technical", Topic="CI/CD nasıl çalışır?",
            Answer="CI/CD automates the build, test, and deployment pipeline. On every commit, CI runs automated tests. If they pass, CD deploys to staging or production — reducing manual work and enabling faster, safer releases." },

        new() { Category="technical", Topic="Docker ve Kubernetes farkı",
            Answer="Docker packages applications into containers. Kubernetes orchestrates those containers at scale — handling scheduling, scaling, self-healing, and load balancing across a cluster of nodes." },

        new() { Category="technical", Topic="CAP teoremi",
            Answer="The CAP theorem states that a distributed system can only guarantee two of three properties: Consistency, Availability, and Partition tolerance. During a network partition you must choose between C and A." },

        new() { Category="technical", Topic="SOLID prensipleri",
            Answer="SOLID stands for: Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, and Dependency Inversion. They guide writing maintainable, extensible object-oriented code." },

        new() { Category="technical", Topic="SQL vs NoSQL",
            Answer="SQL databases use structured schemas and ACID transactions — great for relational data. NoSQL databases offer flexible schemas and horizontal scaling — better suited for large volumes of unstructured or semi-structured data." },

        new() { Category="technical", Topic="API rate limiting",
            Answer="Rate limiting restricts the number of requests a client can make in a time window. It protects services from abuse and ensures fair resource usage. Common strategies are token bucket and sliding window." },

        new() { Category="technical", Topic="Load balancing stratejileri",
            Answer="Common strategies include round-robin, least connections, IP hash, and weighted round-robin. The choice depends on session state requirements, server capacity, and traffic patterns." },

        new() { Category="technical", Topic="Cache stratejileri",
            Answer="Cache-aside loads data on miss; write-through writes to cache and DB simultaneously; write-behind batches DB writes. TTL and eviction policies like LRU control memory usage." },

        new() { Category="technical", Topic="Event sourcing nedir?",
            Answer="Event sourcing stores state changes as an immutable sequence of events rather than current state. You can replay events to reconstruct state, which gives full audit trail and enables time-travel debugging." },

        new() { Category="technical", Topic="TDD yaklaşımı",
            Answer="Test-driven development follows Red-Green-Refactor: write a failing test, write minimal code to pass it, then refactor. It drives better design, higher coverage, and faster feedback." },

        new() { Category="technical", Topic="Observability vs Monitoring",
            Answer="Monitoring tracks predefined metrics and alerts on known failure modes. Observability — through logs, metrics, and traces — lets you ask arbitrary questions about system state and understand unknown failure modes." },

        new() { Category="technical", Topic="Circuit breaker pattern",
            Answer="A circuit breaker detects repeated failures in a downstream service and short-circuits calls to it for a cooldown period. This prevents cascading failures and gives the failing service time to recover." },

        new() { Category="technical", Topic="Database migration stratejisi",
            Answer="I use backwards-compatible migrations: add new columns as nullable, backfill data in a separate step, then add constraints. This allows zero-downtime deployments and easy rollbacks." },

        new() { Category="technical", Topic="Blue-green deployment",
            Answer="Blue-green deployment maintains two identical environments. Traffic shifts from blue (current) to green (new) after validation. Rollback is instant — just redirect traffic back. It eliminates downtime during releases." },

        // ── BEHAVIORAL ──────────────────────────────────────────────────────
        new() { Category="behavioral", Topic="Zor bir teknik karar",
            Answer="In a previous project we had to choose between a monolith and microservices. I led the analysis, evaluated team size, deployment complexity, and data boundaries. We started with a modular monolith and extracted services only where we had clear scaling needs." },

        new() { Category="behavioral", Topic="Deadline baskısında nasıl çalışırsın?",
            Answer="I prioritize ruthlessly — I identify the critical path, cut scope where possible, and communicate risks early to stakeholders. I also make sure the team doesn't accumulate unsustainable debt under pressure." },

        new() { Category="behavioral", Topic="Takım çatışması nasıl çözülür?",
            Answer="I focus on the technical merits rather than personalities. I facilitate a structured discussion, bring data or prototypes to make the decision concrete, and ensure we document the agreed approach and rationale." },

        new() { Category="behavioral", Topic="Hata yaptığında ne yaparsın?",
            Answer="I own the mistake immediately, assess the impact, and focus on the fix first. Then I do a blameless post-mortem to understand root cause and put preventive measures in place so it doesn't happen again." },

        new() { Category="behavioral", Topic="Liderlik deneyimi",
            Answer="I've led cross-functional teams of up to eight engineers. My approach is to set clear technical direction, remove blockers, and create space for people to do their best work — while staying hands-on with the hardest problems." },

        new() { Category="behavioral", Topic="Öğrenme süreci",
            Answer="I learn by building — I prototype new concepts in side projects or sandboxes. I also read widely: release notes, RFCs, and post-mortems from other companies teach me things I wouldn't find in textbooks." },

        new() { Category="behavioral", Topic="Agile/Scrum deneyimi",
            Answer="I've worked in Scrum teams with two-week sprints. I value the retrospective most — it's where we surface process improvements. I've also adapted Kanban for ops work where demand is unpredictable." },

        // ── GENEL ────────────────────────────────────────────────────────────
        new() { Category="general", Topic="Kendinizi tanıtın",
            Answer="I'm a software engineer with a strong background in backend systems and distributed architecture. I enjoy solving complex scalability problems and mentoring engineers. I'm currently focused on building reliable, observable systems." },

        new() { Category="general", Topic="Neden bu şirkete başvurdunuz?",
            Answer="Your engineering blog and the technical problems you're solving at scale are genuinely interesting to me. I also value the culture of engineering excellence I've seen reflected in how your team talks publicly about technical decisions." },

        new() { Category="general", Topic="5 yıl sonra nerede olmak istiyorsunuz?",
            Answer="I want to be a technical leader who can both shape architecture at scale and grow the engineers around me. I'm less interested in a specific title than in taking on problems with meaningful technical and business impact." },

        new() { Category="general", Topic="Güçlü yönleriniz",
            Answer="My strongest areas are system design, translating ambiguous requirements into concrete technical plans, and building shared understanding across teams. I'm also good at identifying risk early and course-correcting before it becomes expensive." },

        new() { Category="general", Topic="Geliştirilmesi gereken yönler",
            Answer="I sometimes go deep on a problem when a shallower solution would be good enough. I'm actively working on recognizing when perfect is the enemy of done and shipping earlier to get real feedback." },

        new() { Category="general", Topic="Maaş beklentisi",
            Answer="I'm open to discussing compensation. I'd like to understand the full package including equity and growth opportunities. What's the budgeted range for this role?" },

        new() { Category="general", Topic="Sorularınız var mı?",
            Answer="Yes — what does the on-call rotation look like for this team? And what's the biggest technical challenge you expect this role to tackle in the first six months?" },
    ];
}
