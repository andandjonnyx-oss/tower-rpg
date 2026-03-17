# PROJECT_MAP

This file is for AI assistants and external developers.  
Its purpose is to explain the structure, responsibility boundaries, and likely file relationships of this Unity project.

---

## 1. Project Summary

This project is a **smartphone non-field RPG built with Unity**.

The core gameplay loop is:

1. The player advances one step in the Tower scene
2. A game event may occur
   - random encounter
   - talk event
   - item acquisition
   - nothing
3. If needed, the game transitions to battle or UI popups
4. After processing, the game returns to tower progression

This project is currently in the **prototype / skeleton-building phase**.  
The current priority is to make the entire gameplay loop work first, then expand data and content later.

---

## 2. Design Philosophy

The project is designed with these priorities:

- Build the skeleton first
- Expand later by adding data
- Separate responsibilities clearly
- Prefer maintainable structure over short-term hacks
- Make the project understandable for AI and external contributors
- Avoid destructive rewrites when editing existing code

---

## 3. Main Gameplay Domains

The project can be understood as four main domains:

### A. Tower Progression
The main progression system where the player moves step by step inside a tower.

Responsibilities:
- Manage current floor and step
- Advance progression
- Trigger event checks
- Open related UI
- Transition to battle when needed

---

### B. Battle
Handles turn-based or event-based combat after an encounter occurs.

Responsibilities:
- Receive selected enemy data
- Run battle logic
- Show battle UI
- Determine win/loss
- Return to tower scene after battle

---

### C. Talk Events
Handles conversation / scenario events that occur at specific points.

Responsibilities:
- Search for events matching floor/step conditions
- Trigger UI/dialog display
- Track whether one-time events already happened
- Allow new events to be added through data

---

### D. Items
Handles item acquisition, inventory display, usage, and equipment.

Responsibilities:
- Generate or receive item acquisition events
- Show pickup popup
- Add acquired item to inventory
- Show item details in item box UI
- Handle use / equip / discard behavior
- Preserve equipment state across scenes

---

## 4. Expected Scene Structure

### Tower Scene
This is the main scene for progression.

Likely responsibilities:
- Step advancement button/input
- Current tower status display
- Event checks after each step
- Item box open button
- Popup spawning
- Transition to battle scene

Likely connected systems:
- EncounterSystem
- TalkEvent system
- Item acquisition system
- Persistent game state manager

---

### Battle Scene
This is the combat scene.

Likely responsibilities:
- Show enemy
- Execute battle flow
- Return result to persistent state
- Transition back to Tower Scene

Likely connected systems:
- Current enemy holder
- Battle manager/controller
- Player status/equipment state

---

## 5. Important Data Types

These are the main data concepts the AI should expect to find.

### Monster
Represents a single monster/enemy.  
Likely a ScriptableObject.

Expected fields:
- ID
- name
- sprite/image
- floor/step appearance range
- encounter weight
- stats

---

### MonsterDatabase
Stores a list of Monster data and returns valid encounter candidates for the current floor/step.

Used by:
- EncounterSystem

---

### TalkEvent
Represents a single talk/conversation event.  
Likely data-driven.

Expected fields:
- ID
- floor
- step
- dialogue/content
- one-time flag
- optional conditions

---

### TalkEventDatabase
Stores and searches TalkEvent data.

Used by:
- talk event trigger / runner systems
- Tower progression flow

---

### Item Data
Represents an item / weapon / magic / consumable.

Expected fields:
- ID
- name
- type
- description
- icon
- sort order
- usable flag
- equippable flag

---

## 6. Important Runtime State

These values likely need to persist across scene changes.

- current floor
- current step
- currently selected encounter enemy
- player inventory
- currently equipped weapon/item
- flags for one-time talk events
- possibly last tower position before battle

When editing code, AI should assume that scene transitions must not destroy important progression state.

---

## 7. Likely High-Importance Classes

These names are based on the current development direction and existing naming patterns.

### EncounterSystem
Purpose:
- Checks whether a battle occurs after step progression
- Queries MonsterDatabase
- Selects battle target
- Triggers scene transition to Battle

Likely depends on:
- MonsterDatabase
- current floor/step state
- battle scene transition code

---

### Monster
Purpose:
- Single enemy data definition

---

### MonsterDatabase
Purpose:
- Search encounter candidates based on floor/step
- Weight-based selection support possible

---

### TalkEvent
Purpose:
- Single talk event definition

---

### TalkEventDatabase
Purpose:
- Store all talk events
- Find matching event(s) for current floor/step

---

### EventTrigger / Runner classes
Purpose:
- Execute or display talk events
- Bridge data and UI

---

### Item-related classes
Purpose:
- Store acquired items
- Open inventory UI
- Select an item
- Perform use/equip/discard action
- Preserve equipment state

---

## 8. Likely Flow Map

### Tower Step Flow
A likely processing flow is:

1. Player presses progress button
2. Step value increases
3. The system checks whether any event should occur
4. Event priority is resolved
   - talk event
   - encounter
   - item
   - none
5. The corresponding UI or scene transition occurs
6. Control returns to the tower flow

AI should confirm actual implementation details from code before changing this logic.

---

### Encounter Flow
Likely flow:

1. Tower progression calls EncounterSystem
2. EncounterSystem checks encounter chance
3. EncounterSystem queries MonsterDatabase
4. A valid Monster is selected
5. Selected enemy data is stored in shared state
6. Scene changes to Battle Scene

---

### Talk Event Flow
Likely flow:

1. Tower progression checks current floor/step
2. TalkEventDatabase is queried
3. Matching event is found
4. Event runner displays text/UI
5. If one-time, event completion is recorded

---

### Item Flow
Likely flow:

1. Item acquisition event occurs
2. Pickup popup appears
3. Player chooses pick up / ignore
4. If picked up, item is added to inventory
5. Inventory UI can later display details
6. Item action buttons depend on item type
   - item: use / discard
   - weapon: equip / discard
   - magic: custom handling
7. Equipped item state is visually reflected in UI

---

## 9. Expected Folder Roles

The exact project structure may differ, but AI should expect something like this:

```text
Assets/
  Scripts/
    Battle/
    Tower/
    Events/
    Items/
    Data/
    UI/
    Managers/

  ScriptableObjects/
    Monsters/
    TalkEvents/
    Items/

  Scenes/
    Tower
    Battle