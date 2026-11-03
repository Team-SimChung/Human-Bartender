using System;

/// <summary>Presentation-only money counter and notice timing, matching the web currency view.</summary>
public sealed class CurrencyNoticeState
{
    public const double CountDuration = .8;
    public const double Lifetime = 2.6;
    public const double FadeDuration = .5;

    double from;

    public int Balance { get; private set; }
    public long Delta { get; private set; }
    public double Elapsed { get; private set; } = Lifetime;
    public bool Active => Elapsed < Lifetime && Delta != 0;
    public float Opacity => (float)Math.Min(1, Math.Max(0, (Lifetime - Elapsed) / FadeDuration));

    public double Displayed
    {
        get
        {
            double t = Math.Min(1, Elapsed / CountDuration);
            return from + ((double)Balance - from) * (1 - Math.Pow(1 - t, 3));
        }
    }

    public void Reset(int balance)
    {
        Balance = balance;
        from = balance;
        Delta = 0;
        Elapsed = Lifetime;
    }

    public void Change(int balance)
    {
        if (balance == Balance) return;

        long change = (long)balance - Balance;
        bool sameDirection = Math.Sign(Delta) == Math.Sign(change);
        double current = Delta != 0 && !sameDirection ? Balance : Displayed;
        Delta = Elapsed < Lifetime && sameDirection ? Delta + change : change;
        from = current;
        Balance = balance;
        Elapsed = 0;
    }

    public void Advance(float deltaTime)
    {
        Elapsed += Math.Min(.1, Math.Max(0, deltaTime));
    }
}
