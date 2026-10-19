using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SphPhysicsTests
{
    const float Dt = 1f / 120f;
    GameObject root;

    [SetUp]
    public void SetUp() => root = new GameObject("sph physics test");

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    [TestCase(0.065f)]
    [TestCase(0.16f)]
    public void LargeForcesCannotMoveAnEntireStepBeforeTheVelocityLimit(float spacing)
    {
        var config = new SphConfig { spacing = spacing };
        var p = Create(config, Vector2.zero);
        p.force = new Vector2(100000f, -100000f);
        p.Integrate(Dt);
        Assert.That(p.pos.magnitude, Is.LessThanOrEqualTo(spacing * 0.4001f));
    }

    [Test]
    public void CoincidentParticlesSeparateWithoutAddingCentreOfMassMotion()
    {
        var config = NoPressure();
        var sim = Simulation(config);
        var a = Create(config, Vector2.zero);
        var b = Create(config, Vector2.zero);
        sim.Register(a);
        sim.Register(b);
        for (int i = 0; i < 30; i++) sim.Tick(Dt);
        Assert.That(Vector2.Distance(a.pos, b.pos), Is.GreaterThan(config.spacing * 0.2f));
        Assert.That((a.pos + b.pos).magnitude, Is.LessThan(0.00001f));
    }

    [Test]
    public void ACloseFluidClusterStillAcceleratesUnderGravity()
    {
        var config = NoPressure();
        config.gravity = 9f;
        var sim = Simulation(config);
        var particles = Grid(sim, config, 3, 3);
        sim.CalibrateRestDensity(1f);
        Vector2 initial = Centre(particles);
        const int steps = 12;
        for (int i = 0; i < steps; i++) sim.Tick(Dt);
        float expectedDrop = config.gravity * Dt * Dt * steps * (steps + 1) * 0.5f;
        Assert.That(Centre(particles).y, Is.EqualTo(initial.y - expectedDrop).Within(0.0001f));
        Assert.That(particles[4].velocity.y, Is.EqualTo(-config.gravity * Dt * steps).Within(0.0001f));
    }

    [Test]
    public void AHeavilyCompressedPatchRecoversWithABoundedBudgetPerStep()
    {
        var config = NoPressure();
        var sim = Simulation(config);
        var particles = Grid(sim, config, 9, 9);
        sim.CalibrateRestDensity(1f);
        foreach (var p in particles) p.pos *= 0.55f;
        float before = MaximumDensity(particles, config.NeighborRadius);
        for (int i = 0; i < 30; i++)
        {
            sim.Tick(Dt);
            Assert.That(sim.LastDensityIterations, Is.InRange(0, 3));
        }
        float after = MaximumDensity(particles, config.NeighborRadius);
        Assert.That(after, Is.LessThan(before * 0.8f));
    }

    [Test]
    public void UncompressedParticlesFinishBeforeUsingAnyCorrectionIterations()
    {
        var config = NoPressure();
        var sim = Simulation(config);
        Grid(sim, config, 5, 5);
        sim.CalibrateRestDensity(1f);
        sim.Tick(Dt);
        Assert.That(sim.LastDensityIterations, Is.Zero);
    }

    [Test]
    public void AParticleThatWasInsideCannotEscapeThroughADistantFloor()
    {
        var p = Create(new SphConfig(), Vector2.zero);
        p.pos = new Vector2(0.3f, -20f);
        p.velocity = Vector2.down * 1000f;
        SphContainerBoundary.Constrain(p, root.transform, Vector2.one, 0.05f);
        Assert.That(p.pos.y, Is.EqualTo(-0.95f).Within(0.00001f));
        Assert.That(p.velocity.y, Is.Zero.Within(0.00001f));
    }

    [Test]
    public void SweepingThroughTheMouthAndFloorStillLandsInside()
    {
        var p = Create(new SphConfig(), new Vector2(0f, 5f));
        p.pos = new Vector2(0f, -10f);
        SphContainerBoundary.Constrain(p, root.transform, Vector2.one, 0.05f);
        Assert.That(p.pos.y, Is.EqualTo(-0.95f).Within(0.00001f));
    }

    [TestCase(1.05f)]
    [TestCase(2f)]
    public void MissingTheMouthDoesNotPullAnOutsideParticleIntoTheGlass(float x)
    {
        var p = Create(new SphConfig(), new Vector2(x, 2f));
        p.pos = new Vector2(x, -0.5f);
        Vector2 expected = p.pos;
        SphContainerBoundary.Constrain(p, root.transform, Vector2.one, 0.05f);
        Assert.That(p.pos, Is.EqualTo(expected));
    }

    [Test]
    public void TheOpenTopAllowsOverflow()
    {
        var p = Create(new SphConfig(), Vector2.zero);
        p.pos = new Vector2(0.3f, 1.2f);
        p.velocity = Vector2.up;
        SphContainerBoundary.Constrain(p, root.transform, Vector2.one, 0.05f);
        Assert.That(p.pos.y, Is.EqualTo(1.2f));
        Assert.That(p.velocity.y, Is.EqualTo(1f));
    }

    [TestCase(1.6f)]
    [TestCase(-1.6f)]
    public void FloorAndWallConstraintsRespectAWorldOffsetRotationAndScale(float scaleX)
    {
        root.transform.position = new Vector3(700f, 600f);
        root.transform.rotation = Quaternion.Euler(0f, 0f, 12f);
        root.transform.localScale = new Vector3(scaleX, 2.7f, 1f);
        var p = Create(new SphConfig(), root.transform.position);
        p.pos = root.transform.TransformPoint(new Vector3(3f, -20f));
        p.velocity = root.transform.TransformVector(new Vector3(100f, -100f));
        SphContainerBoundary.Constrain(p, root.transform, Vector2.one * 0.45f, 0.0325f);
        Vector3 local = root.transform.InverseTransformPoint(p.pos);
        Assert.That(local.x, Is.EqualTo(0.45f - 0.0325f / Mathf.Abs(scaleX)).Within(0.0001f));
        Assert.That(local.y, Is.EqualTo(-0.45f + 0.0325f / 2.7f).Within(0.0001f));
        Assert.That(p.velocity.magnitude, Is.LessThan(0.001f));
    }

    [Test]
    public void RestingOnTheFloorDoesNotRegainDownwardVelocityAfterCorrection()
    {
        var config = new SphConfig();
        var boundary = new SphContainerBoundary(root.transform, Vector2.one, config.spacing * 0.5f);
        var sim = Simulation(config, boundary.Constrain);
        var particle = Create(config, new Vector2(0f, -1f + config.spacing * 0.5f));
        sim.Register(particle);
        for (int i = 0; i < 120; i++) sim.Tick(Dt);
        Assert.That(particle.velocity.y, Is.Zero.Within(0.00001f));
        Assert.That(particle.pos.y, Is.EqualTo(-1f + config.spacing * 0.5f).Within(0.00001f));
        Assert.That((Vector2)particle.transform.position, Is.EqualTo(particle.pos));
    }

    [TestCase("Liquid_Light")]
    [TestCase("Liquid_Default")]
    [TestCase("Liquid_Syrup")]
    public void ASettledColumnKeepsItsVolumeAndParticlesAboveTheFloor(string profileName)
    {
        var profile = AssetDatabase.LoadAssetAtPath<LiquidProfile>(
            "Assets/01.Game/04.Data/01.Definitions/Minigames/Pour/" + profileName + ".asset");
        Assert.That(profile, Is.Not.Null);
        var config = JsonUtility.FromJson<SphConfig>(JsonUtility.ToJson(profile.physics));
        Assert.That(config.densityIterations, Is.EqualTo(3));
        float spacing = config.spacing;
        const int side = 16;
        float initialHeight = side * spacing;
        root.transform.position = new Vector3(0f, initialHeight);
        Vector2 halfExtents = new Vector2(initialHeight * 0.5f, initialHeight);
        var boundary = new SphContainerBoundary(root.transform, halfExtents, spacing * 0.5f);
        var sim = Simulation(config, boundary.Constrain);
        var particles = Grid(sim, config, side, side);
        foreach (var p in particles) p.pos = p.previousPos = new Vector2(p.pos.x, p.pos.y + initialHeight * 0.5f);
        sim.CalibrateRestDensity();
        for (int i = 0; i < 600; i++) sim.Tick(Dt);
        float meanHeight = Centre(particles).y;
        float minY = float.PositiveInfinity;
        foreach (var p in particles)
        {
            Assert.That(float.IsNaN(p.pos.x) || float.IsInfinity(p.pos.y), Is.False);
            minY = Mathf.Min(minY, p.pos.y);
        }
        TestContext.WriteLine("spacing=" + spacing + " mean_height_ratio=" + meanHeight / (initialHeight * 0.5f));
        Assert.That(minY, Is.GreaterThanOrEqualTo(spacing * 0.5f - 0.0001f));
        Assert.That(meanHeight / (initialHeight * 0.5f), Is.InRange(0.8f, 1.5f));
        Assert.That(sim.ActiveParticles.Count, Is.EqualTo(side * side));
    }

    [TestCase(0.065f)]
    [TestCase(0.16f)]
    public void SpatialGridDensitiesMatchAllPairsAtCellEdgesAndOutsideTheGrid(float spacing)
    {
        var config = NoPressure();
        config.spacing = spacing;
        config.densityIterations = 0;
        Vector2 origin = new Vector2(700f, 600f);
        var sim = new SphSimulation(config, origin, origin + Vector2.one);
        var particles = new List<SphParticle>();
        for (int y = -1; y < 5; y++)
        for (int x = -1; x < 5; x++)
        {
            var p = Create(config, origin + new Vector2(x, y) * spacing);
            sim.Register(p);
            particles.Add(p);
        }
        sim.Tick(Dt);
        foreach (var p in particles)
            Assert.That(p.density, Is.EqualTo(Density(particles, p, config.NeighborRadius)).Within(0.00001f));
    }

    static SphConfig NoPressure() => new SphConfig
    {
        gravity = 0f, pressureStiffness = 0f, nearPressureStiffness = 0f,
        viscosity = 0f, velocitySmoothing = 0f, cohesion = 0f
    };

    [Test]
    public void OverlappingAirborneStreamDoesNotLaunchParticlesUpward()
    {
        var config = LoadConfig("Liquid_Light");
        var sim = Simulation(config);
        var particles = Grid(sim, config, 5, 5);
        sim.CalibrateRestDensity();
        foreach (var p in particles)
        {
            p.pos *= 0.4f;
            p.velocity = Vector2.down * 1.6f;
        }
        float largestUpwardVelocity = 0f;
        for (int i = 0; i < 60; i++)
        {
            sim.Tick(Dt);
            foreach (var p in particles) largestUpwardVelocity = Mathf.Max(largestUpwardVelocity, p.velocity.y);
        }
        TestContext.WriteLine("airborne_max_upward_velocity=" + largestUpwardVelocity);
        Assert.That(largestUpwardVelocity, Is.LessThan(0.05f), "Overlap correction must not turn a falling stream into an explosion.");
    }

    [TestCase(0f, 0f)]
    [TestCase(700f, 0f)]
    [TestCase(700f, 100f)]
    [TestCase(700f, 110f)]
    public void PouringIntoOneSideSettlesToALevelSurface(float origin, float bottleAngle)
    {
        var config = LoadConfig("Liquid_Light");
        root.transform.position = new Vector3(origin, origin + 1.215f);
        var boundary = new SphContainerBoundary(root.transform, new Vector2(0.72f, 1.215f), config.spacing * 0.5f);
        var sim = Simulation(config, boundary.Constrain);
        var seed = Grid(sim, config, 5, 5);
        sim.CalibrateRestDensity();
        foreach (var p in seed) { sim.Unregister(p); Object.DestroyImmediate(p.gameObject); }
        var particles = new List<SphParticle>();
        Vector2 mouth = bottleAngle > 0f ? new Vector2(-0.576f, 3.33f) : new Vector2(0.4f, 2.7f);
        Vector2 direction = bottleAngle > 0f ? (Vector2)(Quaternion.Euler(0f, 0f, -bottleAngle) * Vector3.up) : Vector2.down;
        Vector2 across = new Vector2(-direction.y, direction.x);
        const int pouringSteps = 800;
        for (int step = 0; step < pouringSteps + 360; step++)
        {
            if (step < pouringSteps && step % 5 == 0)
            {
                for (int lane = 0; lane < 2; lane++)
                {
                    var p = Create(config, Vector2.one * origin + mouth + across * ((lane - 0.5f) * config.spacing));
                    p.velocity = direction * 1.6f;
                    sim.Register(p);
                    particles.Add(p);
                }
            }
            sim.Tick(Dt);
        }
        var left = new List<float>();
        var right = new List<float>();
        foreach (var p in particles)
        {
            if (p.pos.x - origin < -0.15f) left.Add(p.pos.y - origin);
            if (p.pos.x - origin > 0.15f) right.Add(p.pos.y - origin);
            Assert.That(p.pos.y, Is.GreaterThanOrEqualTo(origin + config.spacing * 0.5f - 0.0001f));
        }
        Assert.That(left.Count, Is.GreaterThan(50), "Liquid must flow across to the opposite side.");
        Assert.That(right.Count, Is.GreaterThan(50));
        left.Sort(); right.Sort();
        float leftSurface = left[(int)((left.Count - 1) * 0.95f)];
        float rightSurface = right[(int)((right.Count - 1) * 0.95f)];
        float gap = Mathf.Abs(leftSurface - rightSurface);
        TestContext.WriteLine("surface_left=" + leftSurface + " right=" + rightSurface + " gap=" + gap);
        Assert.That(gap, Is.LessThan(config.spacing * 1.5f), "A poured liquid must not keep a diagonal sand-like pile.");
    }

    [TestCase(-81.335f)]
    [TestCase(701.38f)]
    public void SmallPourInScaledGlassSettles(float originX)
    {
        var config = LoadConfig("Liquid_Light");
        Vector2 origin = new Vector2(originX,-33.7323f);
        root.transform.position = origin;
        root.transform.localScale = new Vector3(1.6f,2.7f,1);
        var boundary = new SphContainerBoundary(root.transform,Vector2.one*0.45f,config.spacing*0.5f);
        var sim = new SphSimulation(config,origin-Vector2.one*5,origin+Vector2.one*5,boundary.Constrain);
        var seed = Grid(sim,config,5,5);
        foreach (var p in seed) p.pos += origin;
        sim.CalibrateRestDensity();
        foreach (var p in seed) { sim.Unregister(p); Object.DestroyImmediate(p.gameObject); }
        var particles = new List<SphParticle>();
        Vector2 direction = Quaternion.Euler(0,0,-110)*Vector3.up;
        Vector2 across = new Vector2(-direction.y,direction.x);
        for (int step=0;step<1240;step++)
        {
            if (step<280 && step%5==0)
                for (int lane=0;lane<2;lane++)
                {
                    var p = Create(config,origin+new Vector2(-0.576f,2.115f)+across*((lane-0.5f)*config.spacing));
                    p.velocity = direction*1.6f; sim.Register(p); particles.Add(p);
                }
            sim.Tick(Dt);
        }
        var left = new List<float>(); var right = new List<float>();
        foreach (var p in particles)
        {
            if (p.pos.x-origin.x < -0.15f) left.Add(p.pos.y);
            if (p.pos.x-origin.x > 0.15f) right.Add(p.pos.y);
        }
        Assert.That(left.Count,Is.GreaterThan(5)); Assert.That(right.Count,Is.GreaterThan(5));
        left.Sort(); right.Sort();
        float gap = Mathf.Abs(left[(int)((left.Count-1)*0.95f)]-right[(int)((right.Count-1)*0.95f)]);
        TestContext.WriteLine($"small pour gap={gap}, left={left.Count}, right={right.Count}");
        Assert.That(gap,Is.LessThan(config.spacing*1.5f));
    }

    [Test]
    public void RepeatedContainerConstraintsDoNotMoveInteriorParticles()
    {
        root.transform.position = new Vector3(-81.335f,-33.7323f);
        root.transform.localScale = new Vector3(1.6f,2.7f,1f);
        var boundary = new SphContainerBoundary(root.transform,Vector2.one*0.45f,0.0325f);
        for (int x=-3;x<=3;x++)
        {
            Vector2 position = root.transform.TransformPoint(new Vector3(x*0.1f,0.05f));
            var p = Create(new SphConfig(),position);
            for (int i=0;i<1000;i++) boundary.Constrain(p);
            Assert.That(p.pos,Is.EqualTo(position),"A boundary check inside the glass must not create drift.");
        }
    }

    [Test]
    public void FluidViscosityPreservesMomentumAndDoesNotAddEnergy()
    {
        var config = LoadConfig("Liquid_Light");
        config.gravity = 0f;
        config.densityTolerance = 0.2f;
        var sim = Simulation(config);
        var particles = Grid(sim, config, 3, 1);
        sim.CalibrateRestDensity(1f);
        particles[0].velocity = Vector2.left;
        particles[1].velocity = Vector2.zero;
        particles[2].velocity = Vector2.right * 2f;
        sim.Tick(Dt);
        Vector2 sum = Vector2.zero;
        float energy = 0f;
        foreach (var p in particles) { sum += p.velocity; energy += p.velocity.sqrMagnitude; }
        Assert.That(sum.x, Is.EqualTo(1f).Within(0.00001f));
        Assert.That(sum.y, Is.Zero.Within(0.00001f));
        Assert.That(energy, Is.LessThanOrEqualTo(5f));
    }

    static SphConfig LoadConfig(string name)
    {
        var profile = AssetDatabase.LoadAssetAtPath<LiquidProfile>(
            "Assets/01.Game/04.Data/01.Definitions/Minigames/Pour/" + name + ".asset");
        Assert.That(profile, Is.Not.Null);
        return JsonUtility.FromJson<SphConfig>(JsonUtility.ToJson(profile.physics));
    }

    static SphSimulation Simulation(SphConfig config, System.Action<SphParticle> boundary = null) =>
        new SphSimulation(config, Vector2.one * -10f, Vector2.one * 10f, boundary);

    SphParticle Create(SphConfig config, Vector2 pos)
    {
        var body = new GameObject("particle");
        body.transform.SetParent(root.transform, false);
        var p = body.AddComponent<SphParticle>();
        p.Init(config);
        p.Activate(pos);
        return p;
    }

    List<SphParticle> Grid(SphSimulation sim, SphConfig config, int columns, int rows)
    {
        var result = new List<SphParticle>();
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            var p = Create(config, new Vector2(x - (columns - 1) * 0.5f, y - (rows - 1) * 0.5f) * config.spacing);
            result.Add(p);
            sim.Register(p);
        }
        return result;
    }

    static Vector2 Centre(List<SphParticle> particles)
    {
        Vector2 sum = Vector2.zero;
        foreach (var p in particles) sum += p.pos;
        return sum / particles.Count;
    }

    static float MaximumDensity(List<SphParticle> particles, float radius)
    {
        float maximum = 0f;
        foreach (var p in particles)
            maximum = Mathf.Max(maximum, Density(particles, p, radius));
        return maximum;
    }

    static float Density(List<SphParticle> particles, SphParticle p, float radius)
    {
        float density = 0f;
        foreach (var n in particles)
        {
            float q = Mathf.Max(0f, 1f - Vector2.Distance(p.pos, n.pos) / radius);
            density += 2f * q * q;
        }
        return density;
    }
}
