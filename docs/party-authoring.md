# Party Authoring

Party debug content is authored in
`src/party/resources/PartyContent.tres`.

- `CharacterClassResource` owns base stats, action resources, passive
  reactions, and affinities.
- `BattleCharacterResource` owns battler identity, display name, level,
  experience, and a class reference.
- `DebugPartyResource` places character templates in unique rows and slots.

Classes are authoring defaults. Compilation materializes ordinary
`PartyMember` values with full HP and TP; saves remain independent from the
resources.

Compilation rejects missing references, duplicate IDs, duplicate positions,
unknown catalog actions or reactions, invalid slots, and lineups over five
members.
