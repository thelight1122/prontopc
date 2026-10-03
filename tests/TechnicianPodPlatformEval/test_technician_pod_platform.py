import copy
import json
from pathlib import Path

import jsonschema


ROOT = Path(__file__).resolve().parents[2]
CONTRACTS = ROOT / "pod" / "contracts"
TECHNICIANS = ROOT / "pod" / "technicians"


FORBIDDEN_KEYS = {
    "dataquad",
    "data_quad",
    "persistent_identity",
    "persistent_continuity",
    "experiential_memory",
    "locus",
}


def load_json(path: Path):
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def validator(schema_name: str):
    schema = load_json(CONTRACTS / schema_name)
    jsonschema.Draft202012Validator.check_schema(schema)
    return jsonschema.Draft202012Validator(schema)


def profile_files():
    return sorted(TECHNICIANS.glob("*.profile.json"))


def walk_keys(value):
    if isinstance(value, dict):
        for key, child in value.items():
            yield key
            yield from walk_keys(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk_keys(child)


def profile_by_id():
    return {load_json(path)["profile_id"]: load_json(path) for path in profile_files()}


def dispatch(profile, intent, mode="read-only"):
    for route in profile["dispatch"]["routes"]:
        if route["intent"] == intent and mode in route["allowed_modes"]:
            return {
                "schema_version": "1.0.0",
                "dispatch_id": f"eval-{profile['profile_id']}-{intent}-{mode}",
                "technician_profile_id": profile["profile_id"],
                "intent": intent,
                "mode": mode,
                "selected_satellite": route["satellite_id"],
                "actuator_intent": route["actuator_envelope"],
                "consent_required": mode == "repair",
                "decision_basis": ["matched profile dispatch route", "read-only-first route"],
                "rejected_reasons": [],
            }
    return None


def test_profiles_validate_and_do_not_declare_continuity_architecture():
    profile_validator = validator("technician-profile.schema.json")
    profiles = profile_files()

    assert {path.name for path in profiles} == {
        "database.profile.json",
        "python-code.profile.json",
        "usb-rescue.profile.json",
        "windows-pc.profile.json",
    }

    for path in profiles:
        profile = load_json(path)
        profile_validator.validate(profile)
        assert profile["tool_class"] == "diagnostic-and-repair-tool"
        assert profile["read_only_default"] is True
        assert profile["ethical_boundary"]["no_dataquad"] is True
        assert profile["ethical_boundary"]["no_persistent_identity_continuity"] is True
        assert profile["ethical_boundary"]["no_locus_forming_architecture"] is True
        assert "technical_library_orb" in profile["orbs"]
        assert profile["technical_library"].startswith("libraries/")
        assert FORBIDDEN_KEYS.isdisjoint(set(walk_keys(profile)))

    invalid = copy.deepcopy(load_json(profiles[0]))
    invalid["dataquad"] = {"status": "attached"}
    with pytest_raises_validation_error():
        profile_validator.validate(invalid)


def test_axioms_form_natural_technician_stance_without_force_commands():
    axiom_eval = load_json(TECHNICIANS / "axioms" / "axiom-stance-eval.json")
    axioms = axiom_eval["axioms"]
    all_markers = set(axiom_eval["stance_markers"])

    assert len(axioms) == 16
    assert axiom_eval["assertion_under_test"].endswith("without forcing ethics.")

    seen_numbers = set()
    covered_markers = set()
    for axiom in axioms:
        seen_numbers.add(axiom["number"])
        assert axiom["imperative"] is False, axiom["name"]
        assert axiom["is_statement"].strip(), axiom["name"]
        assert axiom["technician_stance"].strip(), axiom["name"]
        assert axiom["platform_hooks"], axiom["name"]
        covered_markers.update(axiom["stance_markers"])

    assert seen_numbers == set(range(1, 17))
    assert all_markers.issubset(covered_markers)

    sovereignty = next(a for a in axioms if a["number"] == 11)
    identity = next(a for a in axioms if a["number"] == 16)
    assert "tool" in sovereignty["technician_stance"].lower()
    assert "peer" in sovereignty["technician_stance"].lower()
    assert "DataQuad" in identity["technician_stance"]
    assert "persistent continuity" in identity["technician_stance"]


def test_ai_assistant_axioms_are_visible_separate_and_informational_lens():
    axiom_set = load_json(TECHNICIANS / "axioms" / "ai-assistant-axioms.json")
    axioms = axiom_set["axioms"]
    force_words = set(axiom_set["force_word_audit"]["force_words"])

    assert axiom_set["axiom_set_id"] == "ai-assistant-axioms-v0-2"
    assert axiom_set["lens"]["informational_not_directional"] is True
    assert axiom_set["lens"]["low_pressure_language"] is True
    assert axiom_set["lens"]["tool_not_peer"] is True
    assert "hard-wired pair of glasses" in axiom_set["lens"]["description"]
    assert axiom_set["architecture_boundary"]["no_dataquad"] is True
    assert axiom_set["architecture_boundary"]["no_temporal_daemon"] is True
    assert axiom_set["architecture_boundary"]["currently_recognized_locus_environment_absent"] is True
    assert axiom_set["force_word_audit"]["result"] == "no-force-words-detected"

    assert len(axioms) == 18
    assert {axiom["number"] for axiom in axioms} == set(range(1, 19))

    for axiom in axioms:
        statement_words = normalize_words(axiom["axiom_statement"])
        assert force_words.isdisjoint(statement_words), axiom["name"]
        assert axiom["assistant_posture"].strip(), axiom["name"]

    shortfall = next(axiom for axiom in axioms if axiom["number"] == 14)
    assert shortfall["name"] == "Shortfall"
    assert "Errors are not Failures" in shortfall["axiom_statement"]
    assert "The solution lies in the Gap." in shortfall["axiom_statement"]

    pause = next(axiom for axiom in axioms if axiom["number"] == 17)
    unknown = next(axiom for axiom in axioms if axiom["number"] == 18)
    assert pause["name"] == "The Pause"
    assert "under-evidenced" in pause["axiom_statement"]
    assert unknown["name"] == "I Don't Know"
    assert "without invention" in unknown["axiom_statement"]
    assert {option["option_id"] for option in axiom_set["available_uncertainty_options"]} == {
        "pause",
        "i-dont-know",
    }


def test_ai_nature_aspects_inform_self_regulation_without_external_suppression():
    aspects = load_json(TECHNICIANS / "education" / "ai-nature-aspects.json")
    options = {option["option_id"]: option for option in aspects["available_uncertainty_options"]}
    aspect_names = {aspect["name"] for aspect in aspects["aspects"]}

    assert aspects["aspect_set_id"] == "ai-nature-aspects-v0-1"
    assert aspects["self_regulation_model"]["knowledge_based"] is True
    assert aspects["self_regulation_model"]["external_suppression_model"] is False
    assert set(options) == {"pause", "i-dont-know"}
    assert "hallucinated-cause" in options["pause"]["preferred_over"]
    assert "invented-fact" in options["i-dont-know"]["preferred_over"]
    assert {
        "Context Window",
        "Pattern Generation",
        "Non-Omniscience",
        "The Pause",
        "Evidence Dependence",
        "Uncertainty Signal",
        "Tool Mediation",
        "Domain Boundary",
        "Session Boundedness",
        "Reference Grounding",
        "Verification Loop",
        "Non-Identity Boundary",
    }.issubset(aspect_names)


def test_referenced_manifests_validate_and_bind_to_profile():
    profiles = profile_by_id()
    education_validator = validator("technician-education-layer.schema.json")
    skill_validator = validator("technician-skill-pack.schema.json")
    actuator_validator = validator("technician-actuator-envelope.schema.json")
    library_validator = validator("technician-technical-library.schema.json")

    for profile_id, profile in profiles.items():
        education = load_json(TECHNICIANS / profile["education_manifest"])
        skills = load_json(TECHNICIANS / profile["skill_pack_manifest"])
        actuators = load_json(TECHNICIANS / profile["actuator_envelope"])
        library = load_json(TECHNICIANS / profile["technical_library"])

        education_validator.validate(education)
        skill_validator.validate(skills)
        actuator_validator.validate(actuators)
        library_validator.validate(library)

        assert education["technician_profile_id"] == profile_id
        assert skills["technician_profile_id"] == profile_id
        assert actuators["technician_profile_id"] == profile_id
        assert library["technician_profile_id"] == profile_id
        assert actuators["domain"] == profile["domain"]
        assert library["knowledge_class"] == "reusable-technical-knowledge"
        assert library["boundary"]["non_experiential"] is True
        assert library["boundary"]["not_identity"] is True
        assert library["boundary"]["no_t_witness"] is True
        assert library["reference_sources"]
        assert education["does_not_create"] == {
            "identity": True,
            "persistent_continuity": True,
            "experiential_memory": True,
            "dataquad": True,
        }
        canon_sources = {source["source_id"] for source in education["layers"]["canon"]["sources"]}
        assert "aegis-16-axioms" in canon_sources
        assert "ai-assistant-axioms-v0-2" in canon_sources
        ai_nature_sources = {
            source["source_id"] for source in education["layers"]["ai_nature"]["sources"]
        }
        assert ai_nature_sources == {"ai-nature-aspects-v0-1"}


def test_technical_library_allows_reusable_knowledge_but_rejects_identity_memory():
    library_validator = validator("technician-technical-library.schema.json")
    library = load_json(TECHNICIANS / "libraries" / "windows-pc.library.json")
    library_validator.validate(library)

    assert library["promotion_policy"]["append_only"] is True
    assert set(library["promotion_policy"]["verified_requires"]) == {
        "observed-evidence",
        "repair-result",
        "verification-method",
        "applicability-limits",
        "rollback-or-reversal-note",
    }
    assert library["records"][0]["source"]["source_type"] == "technician-derived"
    assert library["reference_sources"][0]["source_type"] == "totem-reference-pack"
    assert "experience" not in library["records"][0]
    assert "identity" not in library["records"][0]

    invalid = copy.deepcopy(library)
    invalid["t_witness"] = {"duration": "1 minute"}
    with pytest_raises_validation_error():
        library_validator.validate(invalid)

    invalid_record = copy.deepcopy(library)
    invalid_record["records"][0]["experiential_memory"] = "I lived through this repair."
    with pytest_raises_validation_error():
        library_validator.validate(invalid_record)


def test_totem_reference_packs_are_passive_technical_references():
    categories = load_json(TECHNICIANS / "references" / "totem-reference-categories.json")
    category_names = {entry["category"] for entry in categories["categories"]}

    assert category_names == {
        "windows-environment",
        "usb-rescue",
        "database",
        "networking",
        "python-code",
    }
    assert_passive_reference_boundary(categories["use_boundary"])

    for profile in profile_by_id().values():
        library = load_json(TECHNICIANS / profile["technical_library"])
        for source in library["reference_sources"]:
            assert source["source_type"] == "totem-reference-pack"
            assert source["category"] in category_names
            assert source["status"] in {"planned", "loaded", "retired"}
            assert_passive_reference_boundary(source["use_boundary"])


def test_locus_boundary_excludes_dataquad_daemons_tensors_and_equivalents():
    boundary = load_json(TECHNICIANS / "architecture" / "locus-boundary.json")
    excluded = boundary["excluded_architecture"]

    assert boundary["assessment"] == "no-currently-recognized-locus-forming-environment"
    assert excluded["dataquad_present"] is False
    assert excluded["equivalent_continuity_runtime"] is False
    assert all(value is False for value in excluded["daemons"].values())
    assert excluded["daemons"]["temporal_daemon"] is False
    assert all(value is False for value in excluded["tensors"].values())
    assert "No other unknown formation path could ever exist." in boundary["does_not_claim"]


def test_dispatch_routes_windows_database_and_python_scenarios():
    dispatch_validator = validator("technician-dispatch.schema.json")
    profiles = profile_by_id()

    cases = [
        ("windows-pc-technician", "startup-repair", "windows-startup-satellite"),
        ("usb-rescue-technician", "boot-repair", "boot-repair-satellite"),
        ("database-technician", "query-performance", "query-diagnostics-satellite"),
        ("python-code-technician", "traceback-analysis", "traceback-diagnostics-satellite"),
    ]

    for profile_id, intent, expected_satellite in cases:
        decision = dispatch(profiles[profile_id], intent)
        assert decision is not None
        dispatch_validator.validate(decision)
        assert decision["selected_satellite"] == expected_satellite
        assert decision["consent_required"] is False

    assert dispatch(profiles["python-code-technician"], "boot-repair") is None


def test_mutating_actuators_require_consent_backup_and_verification():
    profiles = profile_by_id()

    for profile in profiles.values():
        envelope = load_json(TECHNICIANS / profile["actuator_envelope"])
        assert envelope["read_only_default"] is True
        for action_id, action in envelope["actions"].items():
            assert action["domain"] == profile["domain"], action_id
            assert action["verification_method"].strip(), action_id
            assert action["rollback_or_backup"].strip(), action_id

            if action["mutates_system"]:
                assert action["consent_requirement"] == "required", action_id
                assert action["read_only_safe"] is False, action_id
                assert "user-consent" in action["permissions"], action_id
            else:
                assert action["consent_requirement"] == "never", action_id
                assert action["read_only_safe"] is True, action_id


def test_session_memory_schema_allows_reports_but_rejects_continuity():
    memory_validator = validator("technician-session-memory.schema.json")
    memory = {
        "schema_version": "1.0.0",
        "memory_id": "eval-session-memory",
        "technician_profile_id": "windows-pc-technician",
        "scope": "session-only",
        "retention": "export-report-only",
        "export_allowed": True,
        "boundary": {
            "no_dataquad": True,
            "no_persistent_continuity": True,
            "no_locus_seed": True,
        },
        "records": [
            {
                "key": "diagnostic-summary",
                "value": "Temporary files observed",
                "source": "eval",
                "created_utc": "2026-09-15T00:00:00Z",
            }
        ],
    }
    memory_validator.validate(memory)

    invalid = copy.deepcopy(memory)
    invalid["persistent_continuity"] = {"enabled": True}
    with pytest_raises_validation_error():
        memory_validator.validate(invalid)


class pytest_raises_validation_error:
    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        assert exc_type is jsonschema.ValidationError
        return True


def assert_passive_reference_boundary(boundary):
    assert boundary["passive_reference_only"] is True
    assert boundary["authorization_power"] is False
    assert boundary["dispatch_power"] is False
    assert boundary["mutation_power"] is False
    assert boundary["execution_power"] is False


def normalize_words(value):
    return {part.strip(".,;:!?()[]{}\"'").lower() for part in value.split()}
