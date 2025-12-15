using Microsoft.Agents.AI.Data;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

internal sealed class RagKnowledgeBase
{
    private const string CollectionName = "product-and-policy-info";
    private const int TopResults = 3;

    private readonly VectorStoreCollection<string, SearchRecord> _collection;

    private RagKnowledgeBase(VectorStoreCollection<string, SearchRecord> collection)
    {
        _collection = collection;
    }

    public static async Task<RagKnowledgeBase> CreateAsync(
        VectorStore vectorStore,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        CancellationToken cancellationToken = default)
    {
        var collection = vectorStore.GetCollection<string, SearchRecord>(
            CollectionName,
            new VectorStoreCollectionDefinition
            {
                EmbeddingGenerator = embeddingGenerator
            });

        await collection.EnsureCollectionExistsAsync(cancellationToken);

        // Upload sample documents into the store.
        var records = new List<SearchRecord>();
        foreach (var doc in GetSampleDocuments())
        {
            records.Add(new SearchRecord
            {
                SourceId = doc.SourceId,
                SourceName = doc.SourceName,
                SourceLink = doc.SourceLink,
                Text = doc.Text,
                TextEmbedding = await embeddingGenerator.GenerateVectorAsync(doc.Text ?? string.Empty, cancellationToken: cancellationToken)
            });
        }

        await collection.UpsertAsync(records, cancellationToken);

        return new RagKnowledgeBase(collection);
    }

    public async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var results = new List<TextSearchProvider.TextSearchResult>();

        await foreach (var r in _collection.SearchAsync(query, TopResults, options: null, cancellationToken: cancellationToken))
        {
            results.Add(new TextSearchProvider.TextSearchResult
            {
                SourceName = r.Record.SourceName,
                SourceLink = r.Record.SourceLink,
                Text = r.Record.Text ?? string.Empty,
                RawRepresentation = r
            });
        }

        return results;
    }

    private static IEnumerable<SampleDocument> GetSampleDocuments()
    {
        yield return new SampleDocument
        {
            SourceId = "weather-sfo-001",
            SourceName = "Weather Snapshot — San Francisco (Demo Data)",
            SourceLink = "https://weather.brighttrail.example/snapshots/sfo",
            Text =
                "San Francisco: Cool and breezy. Morning low clouds (marine layer) clearing by midday. High 17°C, low 11°C. Winds W 20–30 km/h. No significant precipitation expected."
        };

        yield return new SampleDocument
        {
            SourceId = "weather-nyc-001",
            SourceName = "Weather Snapshot — New York City (Demo Data)",
            SourceLink = "https://weather.brighttrail.example/snapshots/nyc",
            Text =
                "New York City: Partly cloudy with a chance of light showers in the afternoon. High 22°C, low 16°C. Winds SW 10–20 km/h. Carry a light rain jacket."
        };

        yield return new SampleDocument
        {
            SourceId = "weather-london-001",
            SourceName = "Weather Snapshot — London (Demo Data)",
            SourceLink = "https://weather.brighttrail.example/snapshots/london",
            Text =
                "London: Mostly cloudy with intermittent drizzle. High 18°C, low 12°C. Winds W 15–25 km/h. Expect damp conditions; waterproof outerwear recommended."
        };

        yield return new SampleDocument
        {
            SourceId = "returns-001",
            SourceName = "BrightTrail Gear — Returns & Exchanges",
            SourceLink = "https://help.brighttrail.example/returns",
            Text =
                "Returns are accepted within 30 days of delivery for unused items with tags attached. Exchanges are free for size changes. Opened hygiene items (water filters, mouthpieces) are final sale. Refunds are issued to the original payment method 3–7 business days after inspection."
        };

        yield return new SampleDocument
        {
            SourceId = "shipping-001",
            SourceName = "BrightTrail Gear — Shipping & Delivery",
            SourceLink = "https://help.brighttrail.example/shipping",
            Text =
                "Orders ship from our warehouse within 1–2 business days. Standard shipping typically arrives in 3–5 business days in the contiguous US. Expedited options are available at checkout. International delivery times vary by destination and may include duties or taxes."
        };

        yield return new SampleDocument
        {
            SourceId = "warranty-001",
            SourceName = "BrightTrail Gear — Warranty & Repairs",
            SourceLink = "https://help.brighttrail.example/warranty",
            Text =
                "We offer a 1-year limited warranty covering manufacturing defects. Normal wear, accidental damage, and UV degradation are not covered. If your item needs repair, submit photos and a short description; our team will respond within 2 business days with next steps."
        };

        yield return new SampleDocument
        {
            SourceId = "tent-care-001",
            SourceName = "AeroShelter 2P Tent — Care & Storage Guide",
            SourceLink = "https://docs.brighttrail.example/products/aeroshelter-2p/care",
            Text =
                "Rinse tent fabric with cool water and wipe with a non-detergent soap. Air dry fully before storage. Avoid machine washing and avoid prolonged sun exposure to reduce coating breakdown. For small tears, use a nylon patch kit and seam sealer."
        };

        yield return new SampleDocument
        {
            SourceId = "membership-001",
            SourceName = "TrailPoints Membership — Benefits Overview",
            SourceLink = "https://help.brighttrail.example/membership/trailpoints",
            Text =
                "Members earn 2 points per $1 spent, receive free standard shipping, and get early access to seasonal sales. Points apply to future purchases but cannot be redeemed for gift cards. Points expire after 12 months of inactivity."
        };
    }

    private sealed class SampleDocument
    {
        public required string SourceId { get; init; }
        public string? SourceName { get; init; }
        public string? SourceLink { get; init; }
        public string? Text { get; init; }
    }

    private sealed class SearchRecord
    {
        // Embedding dimension must match your configured embedding model (e.g. text-embedding-3-large is 3072).
        private const int EmbeddingDimensions = 3072;

        [VectorStoreKey]
        public required string SourceId { get; init; }

        [VectorStoreData]
        public string? SourceName { get; init; }

        [VectorStoreData]
        public string? SourceLink { get; init; }

        [VectorStoreData(IsFullTextIndexed = true)]
        public string? Text { get; init; }

        [VectorStoreVector(EmbeddingDimensions)]
        public ReadOnlyMemory<float> TextEmbedding { get; init; }
    }
}

