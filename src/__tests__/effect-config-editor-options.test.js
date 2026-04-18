import { EffectConfigEditor } from "../components/EffectConfigEditor.js";

describe("EffectConfigEditor decision branch editor options", () => {
    test("inherits triggeringEventOutputs for decision branch abilities", () => {
        const editor = new EffectConfigEditor();
        const inheritedOutputs = [{ name: "countdown", typeName: "Int32" }];
        editor.options = {
            trigger: {
                eventTypes: ["EffectCompleted"],
                relationType: "Any",
                timing: "On",
                validZones: []
            },
            triggeringEventOutputs: inheritedOutputs
        };

        const options = editor.buildDecisionAbilityEditorOptions(null);

        expect(options.lockedTrigger.eventTypes).toEqual(["EffectCompleted"]);
        expect(options.lockedTrigger.relationType).toBe("Any");
        expect(options.lockedTrigger.timing).toBe("On");
        expect(options.lockedTrigger.validZones).toEqual([]);
        expect(options.triggeringEventOutputs).toEqual(inheritedOutputs);
        expect(options.triggeringEventOutputs).not.toBe(inheritedOutputs);
    });

    test("returns empty inherited outputs when parent has no triggeringEventOutputs", () => {
        const editor = new EffectConfigEditor();
        editor.options = {};

        expect(editor.getInheritedTriggeringEventOutputs()).toEqual([]);
    });

    test("shows only keyword parameter for TriggerAbility keyword mode", () => {
        const editor = new EffectConfigEditor();
        const renderContext = {
            hasTriggerAbilityMode: true,
            triggerAbilityMode: editor.normalizeTriggerAbilityMode("ByKeyword")
        };

        expect(editor.shouldRenderEffectParameter({ propertyName: "TriggerMode" }, renderContext)).toBe(true);
        expect(editor.shouldRenderEffectParameter({ propertyName: "AbilityKeyword" }, renderContext)).toBe(true);
        expect(editor.shouldRenderEffectParameter({ propertyName: "AbilityDefinitionIds" }, renderContext)).toBe(false);
        expect(editor.shouldRenderEffectParameter({ propertyName: "TriggerEventType" }, renderContext)).toBe(false);
        expect(editor.shouldRenderEffectParameter({ propertyName: "AutoRandomTargetSelection" }, renderContext)).toBe(true);
    });

    test("shows only event parameter for TriggerAbility event mode", () => {
        const editor = new EffectConfigEditor();
        const renderContext = {
            hasTriggerAbilityMode: true,
            triggerAbilityMode: editor.normalizeTriggerAbilityMode(2)
        };

        expect(renderContext.triggerAbilityMode).toBe("ByEventType");
        expect(editor.shouldRenderEffectParameter({ propertyName: "AbilityKeyword" }, renderContext)).toBe(false);
        expect(editor.shouldRenderEffectParameter({ propertyName: "AbilityDefinitionIds" }, renderContext)).toBe(false);
        expect(editor.shouldRenderEffectParameter({ propertyName: "TriggerEventType" }, renderContext)).toBe(true);
    });
});
