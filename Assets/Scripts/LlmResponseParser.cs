
using UnityEngine;

namespace SensingWorld
{
    public static class LlmResponseParser
    {
        [System.Serializable]
        private class LlmResponse
        {
            public string dialogue;
            public string targetName;
            public int targetInstanceId;
            public string action;
        }

        public struct Result
        {
            public string Dialogue;
            public string TargetName;
            public int InstanceID;
            public GameObject TargetObject;
            public string Action;

            public bool HasTarget => TargetObject != null;
        }

        public static bool TryParse(string response, out Result result)
        {
            result = default;

            if (string.IsNullOrWhiteSpace(response))
                return false;

            // JSON 앞뒤에 불필요한 텍스트가 있는 경우를 대비
            response = response.Trim();

            int start = response.IndexOf('{');
            int end = response.LastIndexOf('}');

            if (start < 0 || end < start)
                return false;

            string json = response.Substring(
                start,
                end - start + 1
            );

            LlmResponse data;

            try
            {
                data = JsonUtility.FromJson<LlmResponse>(json);
            }
            catch (System.Exception)
            {
                return false;
            }

            if (data == null ||
                string.IsNullOrWhiteSpace(data.dialogue) ||
                string.IsNullOrWhiteSpace(data.action))
            {
                return false;
            }

            result.Dialogue = data.dialogue.Trim();
            result.TargetName = data.targetName;
            result.InstanceID = data.targetInstanceId;
            result.Action = data.action.Trim();

            // 대상이 지정된 경우 실제 Unity 객체 조회
            if (!string.IsNullOrWhiteSpace(data.targetName) &&
                data.targetInstanceId != 0 &&
                SensingWorldState.Instance != null &&
                SensingWorldState.Instance.ActiveTargets.TryGetValue(
                    data.targetInstanceId,
                    out SensingWorldState.ActiveTarget target))
            {
                result.TargetObject = target.Target;
            }

            return true;
        }
    }
}