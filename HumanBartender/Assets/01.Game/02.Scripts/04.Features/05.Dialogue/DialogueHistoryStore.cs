using System.Collections.Generic;

/// <summary>이번 바의 단골·튜토리얼 대화 기록의 정본. 창을 닫아도 유지하며 같은 문장의 재발화도 구분한다.</summary>
public sealed class DialogueHistoryStore
{
    public readonly struct Entry
    {
        public readonly string Speaker;
        public readonly string Text;
        public readonly bool IsCurrent;

        public Entry(string speaker, string text, bool isCurrent)
        { Speaker = speaker; Text = text; IsCurrent = isCurrent; }
    }

    readonly List<Entry> entries = new();

    public int Append(string speaker, string text)
    {
        entries.Add(new Entry(speaker, text, true));
        return entries.Count - 1;
    }

    public void Complete(int index)
    {
        var entry = entries[index];
        entries[index] = new Entry(entry.Speaker, entry.Text, false);
    }

    public Entry[] Snapshot(int currentIndex)
    {
        var result = entries.ToArray();
        for (int i = 0; i < result.Length; i++)
            result[i] = new Entry(result[i].Speaker, result[i].Text, result[i].IsCurrent && i == currentIndex);
        return result;
    }
}
