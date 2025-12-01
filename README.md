# RimTalk Event+

This repository contains the source code and XML assets for the RimWorld mod **RimTalk Event+**.

https://steamcommunity.com/sharedfiles/filedetails/?id=3612632140

---

## Compression templates

RimTalk Event+ supports optional text compression for quests and incidents using XML templates.  
These templates are defined as Defs and keyed by the quest/incident `defName`.

### Def type

Templates use the custom def type:

```xml
<RimTalkEventPlus.RimTalkCompressionTemplateDef>

  <defName>RimTalk_Hospitality_Refugee_Quest</defName>

  <!-- The quest or incident def this template targets -->
  <sourceDefName>Hospitality_Refugee</sourceDefName>

  <!-- Currently used to distinguish quests vs incidents -->
  <kind>Quest</kind>

  <!-- Compressed text sent to RimTalk when this quest is active -->
  <!-- Uses the same tokens as the questDescription template -->
  <compressedBody>
The colony already accepted the refugees. [claimInfo]
They will stay for [questDurationTicks_duration] to rest and regroup.
They offer to work and fight for free during that time.
  </compressedBody>

</RimTalkEventPlus.RimTalkCompressionTemplateDef>
```

- `sourceDefName`  
  Must match the quest or incident `defName` (for example `Hospitality_Refugee`).

- `kind`  
  Used internally to separate template categories (for example `"Quest"`).

- `compressedBody`  
  A string that can reuse the same grammar tokens that appear in the original `questDescription` rulepack, such as:
  - `[claimInfo]`
  - `[map_definite]`
  - `[questDurationTicks_duration]`
  - other tokens present in the quest’s `questDescriptionRules`

This lets you shorten very long letters while preserving key facts for the LLM.
