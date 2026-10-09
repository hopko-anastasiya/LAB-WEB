using LabWeb.Data;
using LabWeb.Models;
using LabWeb.Services;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.NGram;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Search;

public record SearchMatch(int PointId, float Score);

public class PointSearchIndex : IDisposable
{
    private const LuceneVersion Version = LuceneVersion.LUCENE_48;
    private const string NgramSuffix = "_ng";

    private static readonly (string Field, float Boost)[] Fields =
    [
        ("name", 3f), ("settlement", 2f), ("services", 1.5f), ("address", 1.2f), ("operator", 1f), ("description", .6f)
    ];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly RAMDirectory _directory = new();
    private readonly Analyzer _analyzer = Analyzer.NewAnonymous((field, reader) =>
    {
        var tokenizer = new StandardTokenizer(Version, reader);
        TokenStream stream = new LowerCaseFilter(Version, tokenizer);
        if (field.EndsWith(NgramSuffix))
            stream = new EdgeNGramTokenFilter(Version, stream, 2, 20);
        return new TokenStreamComponents(tokenizer, stream);
    }, Analyzer.PER_FIELD_REUSE_STRATEGY);
    private readonly IndexWriter _writer;
    private readonly SearcherManager _searchers;

    public PointSearchIndex()
    {
        _writer = new IndexWriter(_directory, new IndexWriterConfig(Version, _analyzer));
        _writer.Commit();
        _searchers = new SearcherManager(_writer, true, null);
    }

    public async Task RebuildAsync(AppDbContext db, CancellationToken ct = default)
    {
        var points = await Load(db).ToListAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            _writer.DeleteAll();
            foreach (var point in points)
                _writer.AddDocument(ToDocument(point));
            Commit();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateAsync(AppDbContext db, IReadOnlyCollection<int> pointIds, CancellationToken ct = default)
    {
        var points = await Load(db).Where(p => pointIds.Contains(p.Id)).ToListAsync(ct);
        await _gate.WaitAsync(ct);
        try
        {
            foreach (var id in pointIds)
            {
                var point = points.FirstOrDefault(p => p.Id == id);
                if (point is null)
                    _writer.DeleteDocuments(new Term("id", id.ToString()));
                else
                    _writer.UpdateDocument(new Term("id", id.ToString()), ToDocument(point));
            }
            Commit();
        }
        finally
        {
            _gate.Release();
        }
    }

    public (IReadOnlyList<SearchMatch> Matches, int Total) Search(string text, PointStatus? status, int skip, int limit)
    {
        var terms = Tokenize(text);
        if (terms.Count == 0) return ([], 0);

        var query = new BooleanQuery();
        foreach (var term in terms)
        {
            var anyField = new BooleanQuery();
            foreach (var (field, boost) in Fields)
            {
                anyField.Add(new TermQuery(new Term(field + NgramSuffix, term)) { Boost = boost }, Occur.SHOULD);
                var edits = term.Length >= 7 ? 2 : term.Length >= 4 ? 1 : 0;
                if (edits > 0)
                    anyField.Add(new FuzzyQuery(new Term(field, term), edits, 1) { Boost = boost * .5f }, Occur.SHOULD);
            }
            query.Add(anyField, Occur.MUST);
        }
        if (status is not null)
            query.Add(new TermQuery(new Term("status", status.ToString())), Occur.MUST);

        _searchers.MaybeRefresh();
        var searcher = _searchers.Acquire();
        try
        {
            var top = searcher.Search(query, skip + limit);
            var matches = top.ScoreDocs.Skip(skip)
                .Select(d => new SearchMatch(int.Parse(searcher.Doc(d.Doc).Get("id")), d.Score))
                .ToList();
            return (matches, top.TotalHits);
        }
        finally
        {
            _searchers.Release(searcher);
        }
    }

    private List<string> Tokenize(string text)
    {
        var terms = new List<string>();
        using var stream = _analyzer.GetTokenStream("name", new StringReader(text));
        var term = stream.AddAttribute<ICharTermAttribute>();
        stream.Reset();
        while (stream.IncrementToken())
            terms.Add(term.ToString());
        stream.End();
        return terms.Distinct().Take(10).ToList();
    }

    private static IQueryable<Point> Load(AppDbContext db) =>
        db.Points.AsNoTracking().Include(p => p.Services).Include(p => p.Operator).AsSplitQuery();

    private static Document ToDocument(Point p)
    {
        var doc = new Document
        {
            new StringField("id", p.Id.ToString(), Field.Store.YES),
            new StringField("status", p.Status.ToString(), Field.Store.NO)
        };
        var values = new Dictionary<string, string?>
        {
            ["name"] = p.Name,
            ["settlement"] = p.Settlement,
            ["address"] = p.Address,
            ["services"] = string.Join(" ", p.Services.Select(s => s.Name)),
            ["operator"] = p.Operator is null ? null : $"{p.Operator.Name} {p.Operator.Organization}",
            ["description"] = p.Description
        };
        foreach (var (field, value) in values)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            doc.Add(new TextField(field, value, Field.Store.NO));
            doc.Add(new TextField(field + NgramSuffix, value, Field.Store.NO));
        }
        return doc;
    }

    private void Commit()
    {
        _writer.Commit();
        _searchers.MaybeRefreshBlocking();
    }

    public void Dispose()
    {
        _searchers.Dispose();
        _writer.Dispose();
        _analyzer.Dispose();
        _directory.Dispose();
        _gate.Dispose();
    }
}

public class SearchIndexUpdater(PointSearchIndex index, AppDbContext db) : IDataChangeHandler
{
    public Task HandleAsync(DataChange change, CancellationToken ct = default) =>
        change.OperatorsOrServicesChanged
            ? index.RebuildAsync(db, ct)
            : index.UpdateAsync(db, change.PointIds, ct);
}

public class SearchIndexInitializer(PointSearchIndex index, IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        await index.RebuildAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
