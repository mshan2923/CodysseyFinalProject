using System.Text.RegularExpressions;
using UnityEngine;

namespace SensingWorld
{
    public static class LlmResponseParser
    {
        private static readonly Regex TargetRegex = new(
            @"Target:\s*(.+?)\s*\(InstanceID:\s*(\d+)\)",
            RegexOptions.Compiled
        );

        public struct Result
        {
            public string Dialogue;
            public string TargetName;
            public int InstanceID;
            public GameObject TargetObject;

            public bool HasTarget => TargetObject != null;
        }

        public static bool TryParse(string response, out Result result)
        {
            result = default;

            if (string.IsNullOrWhiteSpace(response))
                return false;

            // -------------------------
            // 대사 / Target 정보 분리
            // -------------------------

            string[] parts = response.Split(
                new[] { "---" },
                System.StringSplitOptions.None
            );

            result.Dialogue = parts[0].Trim();

            // Target 정보가 없는 경우
            if (parts.Length < 2)
                return true;

            // -------------------------
            // Target 정보 파싱
            // -------------------------

            Match match = TargetRegex.Match(parts[1]);

            if (!match.Success)
                return true;

            result.TargetName = match.Groups[1].Value.Trim();

            if (!int.TryParse(
                match.Groups[2].Value,
                out result.InstanceID))
            {
                return true;
            }

            // -------------------------
            // 실제 Unity 대상 찾기
            // -------------------------

            if (SensingWorldState.Instance != null &&
                SensingWorldState.Instance.ActiveTargets.TryGetValue(
                    result.InstanceID,
                    out SensingWorldState.ActiveTarget target))
            {
                result.TargetObject = target.Target;
            }

            return true;
        }
    }
}