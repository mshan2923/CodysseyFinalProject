using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SensingWorld
{
    public sealed class SensingWorldState : MonoBehaviour
    {
        private static SensingWorldState _instance;
        public static SensingWorldState Instance { get; private set;}

        [SerializeField] private SensingWorldEventBusHost eventBusHost;
        [SerializeField] private InteractionSensor interactionSensor;
        [SerializeField] private int maxRecentEvents = 32;
        [SerializeField] private int maxContextTargets = 12;
        [SerializeField] private bool includeRecentVisionEvents = true;

        private readonly Dictionary<GameObject, ActiveTarget> activeTargets = new();
        private readonly Dictionary<GameObject, GameObject> heldItems = new();
        private readonly List<SensingWorldEvent> recentVisionEvents = new();
        private readonly List<SensingWorldEvent> recentWorldEvents = new();
        private IDisposable eventSubscription;

        public IReadOnlyCollection<ActiveTarget> ActiveTargets => activeTargets.Values;
        public IReadOnlyList<SensingWorldEvent> RecentVisionEvents => recentVisionEvents;
        public IReadOnlyList<SensingWorldEvent> RecentWorldEvents => recentWorldEvents;

        public void ClearRecentEvents()
        {
            recentVisionEvents.Clear();
            recentWorldEvents.Clear();
        }

        private void Awake()
        {
            if(Instance == null)
                Instance = this;

            if (eventBusHost == null)
                eventBusHost = GetComponent<SensingWorldEventBusHost>();

            if (interactionSensor == null)
                interactionSensor = FindFirstObjectByType<InteractionSensor>();
        }

        private void OnEnable()
        {
            TrySubscribeToEventBus();
        }

        private void Start()
        {
            // EventBusHost.Awake() may run after this component's OnEnable().
            TrySubscribeToEventBus();
        }

        private void TrySubscribeToEventBus()
        {
            if (eventSubscription != null)
                return;

            if (eventBusHost == null)
                eventBusHost = GetComponent<SensingWorldEventBusHost>();

            if (eventBusHost?.EventBus == null)
            {
                Debug.LogWarning(
                    "[SensingWorld] EventBus is not initialized yet; subscription will be retried in Start().",
                    this);
                return;
            }

            eventSubscription = eventBusHost.EventBus.Subscribe<SensingWorldEvent>(OnSensingWorldEvent);
        }



        public string BuildContext()
        {
            if (!includeRecentVisionEvents)
                recentVisionEvents.Clear();

            RefreshActiveTargets();
            var context = new StringBuilder();
            context.AppendLine("[Current Sensor State]");
            context.AppendLine("This information comes from game sensors, not a camera image.");

            if (activeTargets.Count == 0)
                context.AppendLine("No Interaction or NPC targets are currently detected.");

            List<ActiveTarget> contextTargets = new(activeTargets.Values);
            contextTargets.Sort(CompareContextTargets);

            int contextTargetCount = Mathf.Min(
                Mathf.Max(0, maxContextTargets),
                contextTargets.Count);

            for (int i = 0; i < contextTargetCount; i++)
            {
                ActiveTarget target = contextTargets[i];
                context.AppendLine(
                    $"{target.Name} (InstanceID: {target.InstanceId}) is a {target.Kind} with state {target.State}. " +
                    $"It is {target.Distance:0.00} meters from the Player, " +
                    $"has a facing dot of {target.FacingDot:0.00}, " +
                    $"and an interaction priority score of {target.InteractionScore:0.00}.");

                foreach (SensingWorldAttribute attribute in target.Attributes ?? new List<SensingWorldAttribute>())
                    context.AppendLine($"{target.Name} {attribute.key}: {attribute.value}.");
            }

            context.AppendLine("[Current Relations]");

            if (heldItems.Count == 0)
                context.AppendLine("The Player is not holding an item.");

            foreach (KeyValuePair<GameObject, GameObject> relation in heldItems)
            {
                if (relation.Key == null)
                    continue;

                string actorName = GetDisplayName(relation.Value, "Player");
                string targetName = GetDisplayName(relation.Key, "Unknown");
                context.AppendLine($"{actorName} is holding {targetName}.");
            }

            if (includeRecentVisionEvents)
                AppendRecentEvents(context, "[Recent Vision Events]", recentVisionEvents);

            AppendRecentEvents(context, "[Recent World Events]", recentWorldEvents);

            return context.ToString();
        }

        private static int CompareContextTargets(ActiveTarget left, ActiveTarget right)
        {
            bool leftIsNpc = string.Equals(left.Kind, "NPC", StringComparison.OrdinalIgnoreCase);
            bool rightIsNpc = string.Equals(right.Kind, "NPC", StringComparison.OrdinalIgnoreCase);

            int npcComparison = rightIsNpc.CompareTo(leftIsNpc);
            if (npcComparison != 0)
                return npcComparison;

            int scoreComparison = right.InteractionScore.CompareTo(left.InteractionScore);
            if (scoreComparison != 0)
                return scoreComparison;

            return left.Distance.CompareTo(right.Distance);
        }

        private CurrentWorldContext CreateContextData()
        {
            var context = new CurrentWorldContext
            {
                sensorDataOnly = true,
                cameraImageAvailable = false
            };

            foreach (ActiveTarget target in activeTargets.Values)
                context.targets.Add(target.ToJsonData());

            foreach (KeyValuePair<GameObject, GameObject> relation in heldItems)
            {
                if (relation.Key == null)
                    continue;

                context.relations.Add(new WorldRelationData
                {
                    type = "holding",
                    actorInstanceId = relation.Value != null ? relation.Value.GetInstanceID() : 0,
                    targetInstanceId = relation.Key.GetInstanceID()
                });
            }

            return context;
        }

        private void RefreshActiveTargets()
        {
            if (interactionSensor == null)
                return;

            foreach (ActiveTarget target in new List<ActiveTarget>(activeTargets.Values))
            {
                if (heldItems.TryGetValue(target.Target, out GameObject holder))
                {
                    target.Position = target.Target.transform.position;
                    target.Distance = holder != null
                        ? Vector3.Distance(holder.transform.position, target.Position)
                        : 0f;
                    target.FacingDot = 0f;
                    target.InteractionScore = 1f;
                    target.State = "Held";
                    target.LastUpdatedTime = Time.time;
                    continue;
                }

                if (target.Target == null ||
                    !interactionSensor.TryGetCurrentData(
                        target.Target,
                        out target.Distance,
                        out target.FacingDot,
                        out target.InteractionScore))
                {
                    if (target.Target != null)
                        activeTargets.Remove(target.Target);

                    continue;
                }

                target.Position = target.Target.transform.position;
                target.LastUpdatedTime = Time.time;

                SensingWorldEntity entity = target.Target.GetComponent<SensingWorldEntity>();
                target.Kind = entity != null ? entity.Kind.ToString() : "Unknown";
                target.State = entity != null ? entity.CurrentState.ToString() : "Unknown";
                target.Attributes = entity != null
                    ? new List<SensingWorldAttribute>(entity.Attributes)
                    : new List<SensingWorldAttribute>();
            }
        }

        private void OnSensingWorldEvent(SensingWorldEvent gameEvent)
        {
            RecordRecentEvent(gameEvent);

            if (gameEvent.Target == null)
                return;

            if (gameEvent.Type == SensingWorldEventType.TargetVisible)
            {
                activeTargets[gameEvent.Target] = new ActiveTarget
                {
                    Target = gameEvent.Target,
                    Name = gameEvent.TargetName,
                    InstanceId = gameEvent.Target.GetInstanceID(),
                    TargetTag = gameEvent.TargetTag,
                    Kind = "Unknown",
                    Position = gameEvent.Position,
                    Distance = gameEvent.Distance,
                    FacingDot = gameEvent.FacingDot,
                    InteractionScore = gameEvent.InteractionScore,
                    LastUpdatedTime = gameEvent.Timestamp,
                    State = "Unknown",
                    Attributes = new List<SensingWorldAttribute>()
                };
            }
            else if (gameEvent.Type == SensingWorldEventType.TargetLost)
            {
                activeTargets.Remove(gameEvent.Target);
            }
            else if (gameEvent.Type == SensingWorldEventType.ObjectPickedUp)
            {
                heldItems[gameEvent.Target] = gameEvent.Actor;

                if (!activeTargets.ContainsKey(gameEvent.Target))
                {
                    activeTargets[gameEvent.Target] = new ActiveTarget
                    {
                        Target = gameEvent.Target,
                        Name = gameEvent.TargetName,
                        InstanceId = gameEvent.Target.GetInstanceID(),
                        TargetTag = gameEvent.Target.tag,
                        Kind = "Unknown",
                        Position = gameEvent.Target.transform.position,
                        Distance = gameEvent.Actor != null
                            ? Vector3.Distance(gameEvent.Actor.transform.position, gameEvent.Target.transform.position)
                            : 0f,
                        FacingDot = 0f,
                        InteractionScore = 1f,
                        LastUpdatedTime = gameEvent.Timestamp,
                        State = "Held",
                        Attributes = new List<SensingWorldAttribute>()
                    };
                }
            }
            else if (gameEvent.Type == SensingWorldEventType.ObjectDropped)
            {
                heldItems.Remove(gameEvent.Target);

                if (interactionSensor == null ||
                    !interactionSensor.TryGetCurrentData(gameEvent.Target, out _, out _, out _))
                {
                    activeTargets.Remove(gameEvent.Target);
                }
            }
        }

        private void RecordRecentEvent(SensingWorldEvent gameEvent)
        {
            if (gameEvent.Channel == SensingWorldEventChannel.Vision &&
                !includeRecentVisionEvents)
            {
                recentVisionEvents.Clear();
                return;
            }

            // HPChanged도 별도 버퍼를 만들지 않고 WorldEvent 순서에 그대로 기록한다.
            // 추후 HP 시스템에서 이벤트를 발행하면 Hit 직후의 결과로 LLM이 해석할 수 있다.
            List<SensingWorldEvent> eventBuffer = gameEvent.Channel == SensingWorldEventChannel.Vision
                ? recentVisionEvents
                : recentWorldEvents;

            eventBuffer.Add(gameEvent);

            int eventLimit = Mathf.Max(1, maxRecentEvents);
            while (eventBuffer.Count > eventLimit)
                eventBuffer.RemoveAt(0);
        }

        private static void AppendRecentEvents(
            StringBuilder context,
            string sectionTitle,
            IReadOnlyList<SensingWorldEvent> events)
        {
            context.AppendLine(sectionTitle);

            if (events.Count == 0)
            {
                context.AppendLine("No recent events have been recorded.");
                return;
            }

            foreach (SensingWorldEvent gameEvent in events)
            {
                string actorName =
                    $"{gameEvent.ActorName} (InstanceID: {GetInstanceId(gameEvent.Actor)})";
                string targetName = gameEvent.Target != null
                    ? $"{gameEvent.TargetName} (InstanceID: {gameEvent.Target.GetInstanceID()})"
                    : gameEvent.TargetName;

                context.AppendLine(
                    $"[{gameEvent.Timestamp:0.00}] {gameEvent.Category}: " +
                    $"{actorName} -> {gameEvent.Type} -> {targetName}.");
            }
        }

        private static string GetDisplayName(GameObject entityObject, string fallback)
        {
            if (entityObject == null)
                return fallback;

            SensingWorldEntity entity = entityObject.GetComponent<SensingWorldEntity>();
            return entity != null && !string.IsNullOrWhiteSpace(entity.DisplayName)
                ? entity.DisplayName
                : entityObject.name;
        }

        private static int GetInstanceId(GameObject entityObject)
        {
            return entityObject != null ? entityObject.GetInstanceID() : 0;
        }

        private void OnDisable()
        {
            eventSubscription?.Dispose();
            eventSubscription = null;
            activeTargets.Clear();
            heldItems.Clear();
            ClearRecentEvents();
        }

        [Serializable]
        public sealed class ActiveTarget
        {
            public GameObject Target;
            public string Name;
            public int InstanceId;
            public string TargetTag;
            public string Kind;
            public Vector3 Position;
            public float Distance;
            public float FacingDot;
            public float InteractionScore;
            public float LastUpdatedTime;
            public string State;
            public List<SensingWorldAttribute> Attributes;

            public WorldTargetData ToJsonData()
            {
                return new WorldTargetData
                {
                    instanceId = InstanceId,
                    name = Target != null ? Name : "Unknown",
                    tag = TargetTag,
                    kind = Kind,
                    state = State,
                    position = Position,
                    distance = Distance,
                    facingDot = FacingDot,
                    interactionScore = InteractionScore,
                    attributes = Attributes ?? new List<SensingWorldAttribute>()
                };
            }
        }
    }

    [Serializable]
    public sealed class CurrentWorldContext
    {
        public bool sensorDataOnly;
        public bool cameraImageAvailable;
        public List<WorldTargetData> targets = new();
        public List<WorldRelationData> relations = new();
    }

    [Serializable]
    public sealed class WorldTargetData
    {
        public int instanceId;
        public string name;
        public string tag;
        public string kind;
        public string state;
        public Vector3 position;
        public float distance;
        public float facingDot;
        public float interactionScore;
        public List<SensingWorldAttribute> attributes = new();
    }

    [Serializable]
    public sealed class WorldRelationData
    {
        public string type;
        public int actorInstanceId;
        public int targetInstanceId;
    }
}
