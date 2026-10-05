using System;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "GameEvent", menuName = "Game Events/GameEvent")]
public class GameEvent<T> : ScriptableObject
{
    readonly struct ListenerRegistration
    {
        public readonly GameEventListener<T> Listener;
        public readonly ulong Id;

        public ListenerRegistration(GameEventListener<T> listener, ulong id)
        {
            Listener = listener;
            Id = id;
        }
    }

    readonly List<ListenerRegistration> registrations = new();
    readonly List<ListenerRegistration> dispatchSnapshot = new();
    readonly Queue<T> pendingEvents = new();

    ulong nextRegistrationId;
    bool isPublishing;

    /// <summary>
    /// 현재 이벤트를 최근 등록된 리스너부터 전달한다.
    /// 같은 채널에서 중첩 발행한 이벤트는 현재 발행이 끝난 뒤 요청 순서대로 전달한다.
    /// </summary>
    public void Raise(T data)
    {
        pendingEvents.Enqueue(data);
        if (isPublishing) return;

        isPublishing = true;
        try
        {
            while (pendingEvents.Count > 0)
            {
                T pendingData = pendingEvents.Dequeue();
                Publish(pendingData);
            }
        }
        finally
        {
            isPublishing = false;
            dispatchSnapshot.Clear();
        }
    }

    public void RegisterListener(GameEventListener<T> listener)
    {
        if (listener == null) return;

        RemoveInvalidListeners();
        if (ContainsListener(listener)) return;

        nextRegistrationId++;
        if (nextRegistrationId == 0) nextRegistrationId++;

        registrations.Add(new ListenerRegistration(listener, nextRegistrationId));
    }

    public void UnregisterListener(GameEventListener<T> listener)
    {
        for (int i = registrations.Count - 1; i >= 0; i--)
        {
            GameEventListener<T> registeredListener = registrations[i].Listener;
            if (registeredListener == null || ReferenceEquals(registeredListener, listener))
                registrations.RemoveAt(i);
        }
    }

    void Publish(T data)
    {
        RemoveInvalidListeners();
        dispatchSnapshot.Clear();

        for (int i = 0; i < registrations.Count; i++)
            dispatchSnapshot.Add(registrations[i]);

        for (int i = dispatchSnapshot.Count - 1; i >= 0; i--)
        {
            ListenerRegistration registration = dispatchSnapshot[i];
            GameEventListener<T> listener = registration.Listener;

            if (listener == null || !ContainsRegistration(registration)) continue;

            try
            {
                listener.OnEventRaised(data);
            }
            catch (Exception error)
            {
                Debug.LogError(
                    $"[GameEvent] '{name}' 발행 중 '{listener.name}' 리스너에서 오류가 발생했습니다. " +
                    "나머지 리스너 발행은 계속합니다.",
                    listener);
                Debug.LogException(error, listener);
            }
        }
    }

    bool ContainsListener(GameEventListener<T> listener)
    {
        for (int i = 0; i < registrations.Count; i++)
        {
            if (ReferenceEquals(registrations[i].Listener, listener)) return true;
        }

        return false;
    }

    bool ContainsRegistration(ListenerRegistration registration)
    {
        for (int i = 0; i < registrations.Count; i++)
        {
            ListenerRegistration current = registrations[i];
            if (current.Id == registration.Id && ReferenceEquals(current.Listener, registration.Listener))
                return true;
        }

        return false;
    }

    void RemoveInvalidListeners()
    {
        for (int i = registrations.Count - 1; i >= 0; i--)
        {
            if (registrations[i].Listener == null)
                registrations.RemoveAt(i);
        }
    }
}
