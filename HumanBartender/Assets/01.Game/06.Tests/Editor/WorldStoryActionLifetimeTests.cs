using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class WorldStoryActionLifetimeTests
{
    SceneSetup[] setup;
    bool done;
    Exception failure;
    async UniTask Observe(WorldStoryActors group, OutsideStoryWorldAction kind, Transform player, CancellationToken token)
    {
        done = false; failure = null;
        try { await group.PlayAsync(kind, player, null, token); }
        catch (Exception error) { failure = error; }
        finally { done = true; }
    }
    static WorldStoryActors Build(out Transform samho, out Transform player)
    {
        var host = new GameObject("World story lifetime test");
        var group = host.AddComponent<WorldStoryActors>();
        var speakers = host.AddComponent<WorldDialogueSpeakers>();
        var actors = new[] { "boobi", "neighbor", "repair", "chris", "samho", "dealer", "thug" }.Select((id, i) =>
        {
            var go = new GameObject(id);
            go.transform.SetParent(host.transform, false);
            go.transform.position = new Vector3(i, 0, 0);
            return new WorldStoryActors.Actor { Id = id, Root = go.transform, Renderer = go.AddComponent<SpriteRenderer>() };
        }).ToArray();
        speakers.Configure(actors.Select(actor => (actor.Id, actor.Root)).ToArray());
        group.Configure(actors, speakers);
        samho = actors.Single(actor => actor.Id == "samho").Root;
        player = new GameObject("test player").transform;
        player.SetParent(host.transform, false);
        return group;
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (setup != null && setup.Any(scene => scene.isLoaded && scene.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTest]
    public IEnumerator WorldMotionCancellationDisableAndEveryAuthoredActionReleaseTheirPoses()
    {
        setup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var group = Build(out var samho, out var player);
        var start = samho.position;
        group.BeginSession();
        using var source = new CancellationTokenSource();
        Observe(group, OutsideStoryWorldAction.SamhoDepart, player, source.Token).Forget();
        yield return null;
        yield return null;
        Assert.That(group.IsPlaying, Is.True);
        Assert.That(samho.position, Is.Not.EqualTo(start));
        source.Cancel();
        float until = Time.realtimeSinceStartup + 5;
        while (!done && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(done, Is.True);
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        Assert.That(samho.position, Is.EqualTo(start));
        Assert.That(group.IsPlaying, Is.False);
        group.EndSession();

        group.BeginSession();
        Observe(group, OutsideStoryWorldAction.NeighborEnter, player, CancellationToken.None).Forget();
        group.gameObject.SetActive(false);
        until = Time.realtimeSinceStartup + 5;
        while (!done && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(done, Is.True);
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        group.gameObject.SetActive(true);

        foreach (OutsideStoryWorldAction action in Enum.GetValues(typeof(OutsideStoryWorldAction)))
        {
            if (action == OutsideStoryWorldAction.DoorCue) continue; // existing sound service owns fire-and-forget audio
            group.BeginSession();
            Observe(group, action, player, CancellationToken.None).Forget();
            until = Time.realtimeSinceStartup + 5;
            while (!done && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(done, Is.True, action.ToString());
            Assert.That(failure, Is.Null, action.ToString());
            Assert.That(group.IsPlaying, Is.False);
            group.EndSession();
            Assert.That(samho.position, Is.EqualTo(start));
            Assert.That(samho.GetComponent<SpriteRenderer>().enabled, Is.True);
        }
        Object.Destroy(group.gameObject);
        yield return new ExitPlayMode();
    }
}
