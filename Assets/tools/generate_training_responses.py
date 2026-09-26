import argparse
import json
import os
import tempfile
import time
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


def read_jsonl(path: Path):
    with path.open("r", encoding="utf-8-sig") as input_file:
        for line_number, line in enumerate(input_file, start=1):
            if not line.strip():
                continue

            try:
                yield json.loads(line)
            except json.JSONDecodeError as error:
                raise ValueError(f"입력 JSONL {line_number}번째 줄을 읽을 수 없습니다: {error}") from error


def load_system_prompt(path: Path | None) -> str:
    if path is None:
        return """너는 고양이 NPC다.
        입력된 사건을 해설하거나 분석하지 마라.
        각 응답은 고양이가 직접 말하는 대사여야 한다.

        관찰자 시점과 3인칭 설명을 사용하지 마라.
        "~하는 모습이", "~하네요", "~것 같다" 같은 해설자 표현을 쓰지 마라.

        고양이의 감정과 반응을 자연스러운 한국어 한 문장으로 표현하라.
        각 응답은 서로 다른 말투나 감정을 가져야 한다.

        응답은 JSON 문자열 배열만 반환하라."""

    return path.read_text(encoding="utf-8")


def build_user_prompt(bundle: dict, response_count: int) -> str:
    bundle_json = json.dumps(bundle, ensure_ascii=False, indent=2)
    return (
        f"다음은 게임 플레이 이벤트 한 묶음이다.\n\n"
        f"{bundle_json}\n\n"
        f"이 묶음에 대한 서로 다른 반응을 {response_count}개 생성하라.\n"
        "반응은 각각 독립적인 한 문장으로 작성하라.\n"
        f"반드시 {{\"responses\": [문자열 목록]}} 형식의 JSON만 반환하라. 목록 원소 수는 최대 {response_count}개로 하라."
    )


def call_openai(endpoint, api_key, model, system_prompt, user_prompt, response_count, timeout, retries):
    messages = []
    if system_prompt.strip():
        messages.append({"role": "system", "content": system_prompt})
    messages.append({"role": "user", "content": user_prompt})

    payload = json.dumps(
        {
            "model": model,
            "messages": messages,
            "reasoning_effort": "none",
            "max_completion_tokens": max(512, response_count * 120),
            "response_format": {
                "type": "json_schema",
                "json_schema": {
                    "name": "cat_reactions",
                    "strict": True,
                    "schema": {
                        "type": "object",
                        "properties": {
                            "responses": {
                                "type": "array",
                                "items": {"type": "string"},
                                "minItems": 1,
                                "maxItems": response_count,
                            }
                        },
                        "required": ["responses"],
                        "additionalProperties": False,
                    },
                },
            },
        },
        ensure_ascii=False,
    ).encode("utf-8")
    request = Request(
        endpoint,
        data=payload,
        headers={
            "Content-Type": "application/json",
            "Authorization": f"Bearer {api_key}",
        },
        method="POST",
    )

    last_error = None
    for attempt in range(retries + 1):
        try:
            with urlopen(request, timeout=timeout) as response:
                response_data = json.loads(response.read().decode("utf-8"))

            content = response_data.get("choices", [{}])[0].get("message", {}).get("content", "")
            if not content.strip():
                raise ValueError("OpenAI 응답의 message.content가 비어 있습니다.")
            return content
        except (HTTPError, URLError, TimeoutError, json.JSONDecodeError, ValueError) as error:
            last_error = error
            if attempt < retries:
                time.sleep(2 ** attempt)

    raise RuntimeError(f"OpenAI API 호출 실패: {last_error}") from last_error


def parse_response_array(content: str, response_count: int):
    cleaned = content.strip()
    if cleaned.startswith("```"):
        lines = cleaned.splitlines()
        if lines and lines[0].startswith("```"):
            lines = lines[1:]
        if lines and lines[-1].strip() == "```":
            lines = lines[:-1]
        cleaned = "\n".join(lines).strip()

    parsed = json.loads(cleaned)
    if isinstance(parsed, dict):
        parsed = parsed.get("responses")
    if not isinstance(parsed, list):
        raise ValueError("OpenAI 응답이 responses 배열이 아닙니다.")

    responses = [item.strip() for item in parsed if isinstance(item, str) and item.strip()]
    return responses[:response_count]


def save_pretty_json(path: Path, data: dict):
    path.parent.mkdir(parents=True, exist_ok=True)
    file_descriptor, temporary_name = tempfile.mkstemp(
        prefix=f"{path.stem}_",
        suffix=".tmp",
        dir=path.parent,
        text=True,
    )
    try:
        with os.fdopen(file_descriptor, "w", encoding="utf-8", newline="\n") as output_file:
            json.dump(data, output_file, ensure_ascii=False, indent=2)
            output_file.write("\n")
        os.replace(temporary_name, path)
    finally:
        if os.path.exists(temporary_name):
            os.remove(temporary_name)


def main():
    parser = argparse.ArgumentParser(description="OpenAI API로 이벤트 묶음별 반응을 생성합니다.")
    parser.add_argument("--input", required=True, type=Path, help="Unity가 생성한 JSONL TXT 파일")
    parser.add_argument("--output", required=True, type=Path, help="예쁜 JSON 출력 파일")
    parser.add_argument("--model", default="gpt-6-luna", help="사용할 OpenAI 모델 이름")
    parser.add_argument("--count", type=int, default=10, help="묶음마다 생성할 최대 반응 수")
    parser.add_argument("--system-prompt-file", type=Path, help="선택적 시스템 프롬프트 파일")
    parser.add_argument("--endpoint", default="https://api.openai.com/v1/chat/completions")
    parser.add_argument("--timeout", type=float, default=600.0)
    parser.add_argument("--retries", type=int, default=2)
    args = parser.parse_args()

    if args.count < 1:
        parser.error("--count는 1 이상이어야 합니다.")

    api_key = os.environ.get("OPENAI_API_KEY")
    if not api_key:
        parser.error("OPENAI_API_KEY 환경변수가 설정되어 있지 않습니다.")

    system_prompt = load_system_prompt(args.system_prompt_file)
    results = {}
    if args.output.exists():
        with args.output.open("r", encoding="utf-8") as output_file:
            existing = json.load(output_file)
            if not isinstance(existing, dict):
                raise ValueError("기존 출력 JSON의 최상위 값은 객체여야 합니다.")
            results.update(existing)

    for index, bundle in enumerate(read_jsonl(args.input), start=1):
        bundle_id = str(bundle.get("bundleId", f"bundle-{index}"))
        if bundle_id in results:
            print(f"[{index}] 건너뜀: {bundle_id}")
            continue

        print(f"[{index}] 처리 중: {bundle_id}")
        try:
            content = call_openai(
                args.endpoint,
                api_key,
                args.model,
                system_prompt,
                build_user_prompt(bundle, args.count),
                args.count,
                args.timeout,
                args.retries,
            )
            responses = parse_response_array(content, args.count)
            results[bundle_id] = {"bundle": bundle, "responses": responses}
            print(f"[{index}] 완료: {len(responses)}개 반응")
        except Exception as error:
            results[bundle_id] = {
                "bundle": bundle,
                "responses": [],
                "error": str(error),
            }
            print(f"[{index}] 실패: {error}")

        save_pretty_json(args.output, results)

    print(f"저장 완료: {args.output}")


if __name__ == "__main__":
    main()
