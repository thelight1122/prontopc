"""
Pronto PC — AI Engine
Sends diagnostic data to the Pronto server, which queries Gemini and returns
a structured repair plan.
"""
import os
import requests

PRONTO_SERVER = os.environ.get("PRONTO_SERVER", "https://pronto-pc-108655745293.us-central1.run.app")
REPAIR_PLAN_URL = f"{PRONTO_SERVER}/api/repair-plan"


class AIEngine:

    def get_repair_plan(self, diag_data: dict, session_token: str) -> dict:
        try:
            resp = requests.post(
                REPAIR_PLAN_URL,
                json={"session": session_token, "diagnostics": diag_data},
                headers={"Authorization": f"Bearer {session_token}"},
                timeout=180,
            )
            resp.raise_for_status()
            result = resp.json()
            if not result.get("ok"):
                raise RuntimeError(result.get("error", "Server returned not-ok"))
            return result["plan"]
        except requests.RequestException as e:
            raise RuntimeError(f"Pronto server unreachable: {e}")
