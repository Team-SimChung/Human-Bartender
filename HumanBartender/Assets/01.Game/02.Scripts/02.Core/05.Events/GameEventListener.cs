using UnityEngine;
using UnityEngine.Events;

public class GameEventListener<T> : MonoBehaviour
{
    [SerializeField] private GameEvent<T> channel;
    [SerializeField] private UnityEvent<T> response = new();

    GameEvent<T> registeredChannel;

    private void OnEnable()
    {
        UnregisterFromCurrentChannel();

        if (channel == null)
        {
            Debug.LogError(
                $"[GameEventListener] '{name}'의 {GetType().Name}에 이벤트 채널이 연결되지 않았습니다.",
                this);
            return;
        }

        registeredChannel = channel;
        registeredChannel.RegisterListener(this);
    }

    private void OnDisable()
    {
        UnregisterFromCurrentChannel();
    }

    public void OnEventRaised(T data)
    {
        if (response == null)
        {
            Debug.LogError(
                $"[GameEventListener] '{name}'의 {GetType().Name}에 응답 이벤트가 없습니다.",
                this);
            return;
        }

        response.Invoke(data);
    }

    void UnregisterFromCurrentChannel()
    {
        GameEvent<T> channelToUnregister = registeredChannel;
        registeredChannel = null;

        if (channelToUnregister == null) return;
        channelToUnregister.UnregisterListener(this);
    }
}