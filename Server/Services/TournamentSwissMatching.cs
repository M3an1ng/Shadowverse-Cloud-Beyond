namespace ShadowverseBeyond.Server.Services;

internal static class TournamentSwissMatching
{
    // Edmonds' blossom algorithm finds a maximum matching in an undirected graph.
    public static int[] Find(int[][] edges, int first)
    {
        var count = edges.Length;
        var partner = Enumerable.Repeat(-1, count).ToArray();
        var parent = new int[count];
        var representative = new int[count];
        var queued = new bool[count];
        var contracted = new bool[count];
        var ancestors = new bool[count];
        var queue = new Queue<int>(count);

        if (first >= 0) Augment(first);
        for (var root = 0; root < count; root++)
        {
            if (partner[root] < 0) Augment(root);
        }
        return partner;

        void Augment(int root)
        {
            var endpoint = FindPath(root);
            while (endpoint >= 0)
            {
                var previous = parent[endpoint];
                var next = partner[previous];
                partner[endpoint] = previous;
                partner[previous] = endpoint;
                endpoint = next;
            }
        }

        int FindPath(int root)
        {
            Array.Fill(parent, -1);
            Array.Clear(queued);
            for (var index = 0; index < count; index++) representative[index] = index;
            queue.Clear();
            Enqueue(root);
            while (queue.TryDequeue(out var vertex))
            {
                foreach (var neighbor in edges[vertex])
                {
                    if (representative[vertex] == representative[neighbor] || partner[vertex] == neighbor) continue;
                    if (neighbor == root || partner[neighbor] >= 0 && parent[partner[neighbor]] >= 0)
                    {
                        Contract(vertex, neighbor);
                    }
                    else if (parent[neighbor] < 0)
                    {
                        parent[neighbor] = vertex;
                        if (partner[neighbor] < 0) return neighbor;
                        Enqueue(partner[neighbor]);
                    }
                }
            }
            return -1;
        }

        void Enqueue(int vertex)
        {
            if (queued[vertex]) return;
            queued[vertex] = true;
            queue.Enqueue(vertex);
        }

        void Contract(int one, int two)
        {
            Array.Clear(ancestors);
            var common = representative[one];
            while (true)
            {
                ancestors[common] = true;
                if (partner[common] < 0) break;
                common = representative[parent[partner[common]]];
            }
            common = representative[two];
            while (!ancestors[common]) common = representative[parent[partner[common]]];

            Array.Clear(contracted);
            MarkPath(one, common, two);
            MarkPath(two, common, one);
            for (var index = 0; index < count; index++)
            {
                if (!contracted[representative[index]]) continue;
                representative[index] = common;
                Enqueue(index);
            }
        }

        void MarkPath(int vertex, int common, int previous)
        {
            while (representative[vertex] != common)
            {
                var paired = partner[vertex];
                contracted[representative[vertex]] = contracted[representative[paired]] = true;
                parent[vertex] = previous;
                previous = paired;
                vertex = parent[paired];
            }
        }
    }
}
