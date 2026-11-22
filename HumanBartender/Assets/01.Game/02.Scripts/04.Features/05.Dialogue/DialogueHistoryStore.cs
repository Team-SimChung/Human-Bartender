using System.Collections.Generic;

/// <summary>이번 바에서 플레이어가 본 대화 기록의 정본. 창을 닫아도 유지하며 같은 문장의 재발화도 구분한다.</summary>
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

    /// <summary>말풍선 하나가 지금 말하는 줄이 어느 기록에 몇 글자까지 반영됐는지 기억한다.</summary>
    public sealed class Line
    {
        public int Session { get; internal set; }
        public int Entry { get; internal set; } = -1;
        public int Recorded { get; internal set; }
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

    void Replace(int index, string text)
    {
        var entry = entries[index];
        entries[index] = new Entry(entry.Speaker, text, entry.IsCurrent);
    }

    /// <summary>말풍선의 줄이 바뀌었으면 앞 줄의 기록을 끝내고 새 줄을 처음부터 센다. session 0은 말하는 줄이 없음.</summary>
    public void Track(Line line, int session)
    {
        if (line.Session == session) return;
        if (line.Entry >= 0) Complete(line.Entry);
        line.Session = session;
        line.Entry = -1;
        line.Recorded = 0;
    }

    /// <summary>
    /// 지금 화면에 보이는 글자까지를 기록한다. 전에 반영한 것보다 더 보였을 때만 같은 기록을 늘리며,
    /// 아직 한 글자도 보지 않은 줄은 기록을 만들지 않는다.
    /// </summary>
    public void See(Line line, string speaker, int visible, string visibleText)
    {
        if (line.Session == 0 || visible <= line.Recorded || string.IsNullOrWhiteSpace(visibleText)) return;
        line.Recorded = visible;
        if (line.Entry < 0) line.Entry = Append(speaker, visibleText);
        else Replace(line.Entry, visibleText);
    }

    /// <summary>
    /// 줄이 끝났다. fullText를 주면(단골·튜토리얼 대사) 본 글자와 관계없이 전문으로 남기고,
    /// 비워 두면(손님 말풍선) 본 글자까지만 남긴다.
    /// </summary>
    public void End(Line line, string speaker, string fullText = null)
    {
        if (line.Session == 0) return;
        if (!string.IsNullOrWhiteSpace(fullText))
        {
            if (line.Entry < 0) line.Entry = Append(speaker, fullText);
            else Replace(line.Entry, fullText);
            line.Recorded = int.MaxValue;
        }
        if (line.Entry >= 0) Complete(line.Entry);
    }

    public Entry[] Snapshot(int currentIndex)
    {
        var result = entries.ToArray();
        for (int i = 0; i < result.Length; i++)
            result[i] = new Entry(result[i].Speaker, result[i].Text, result[i].IsCurrent && i == currentIndex);
        return result;
    }
}
