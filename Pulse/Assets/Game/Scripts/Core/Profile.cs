using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Pulse
{
    // What the player keeps between runs: Beats, the parts owned, how far each frame has been
    // upgraded, which rooms of the Roundhouse are open, and the machines built from all that.
    //
    // It saves itself. Everything that changes it calls Save, which only notes that something
    // changed; the Autosave object writes the file within a second, and at once when the game is
    // left. The file is written next to its old self and then put in its place, and the old one is
    // kept as a backup, so a crash in the middle of saving loses nothing.
    public static class Profile
    {
        [Serializable]
        sealed class SavedMachine
        {
            public string name, frame, reactor, drive;
            public List<string> slots = new List<string>(), modules = new List<string>();
            public List<int> items = new List<int>();
        }

        [Serializable]
        sealed class Saved
        {
            public int version, beats, current, nextItem;
            public List<string> found = new List<string>(), fresh = new List<string>(), unlocked = new List<string>();
            public List<string> markedFrames = new List<string>();
            public List<int> marks = new List<int>();
            public List<Item> items = new List<Item>();
            public List<SavedMachine> machines = new List<SavedMachine>();
            // The Codex: what the player has met, what the Archivist has paid for, and how often
            // each thing happened. And the dungeons cleared at least once.
            public List<string> known = new List<string>(), paid = new List<string>(), cleared = new List<string>();
            public List<string> tallied = new List<string>();
            public List<int> tallies = new List<int>();
        }

        // Raised when what a profile holds changes its meaning.
        //   2: frames are bought, and the first frame is the Buggy. Machines saved before that were
        //      built on a frame the player no longer starts with, so they are not read back.
        //   3: parts are items with a level and an ability, slots have their new names, and the
        //      profile is a file. Every kind of part an older profile had found becomes one plain item.
        const int Version = 3;
        public const int MaxPresets = 2;
        public const string DefaultKey = "pulse.profile";

        // The name of the save. Tests point this somewhere else, so they never touch a real one.
        public static string Key = DefaultKey;

        // Every start of the game begins with the player's own profile, whatever a test left behind.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void StartOver()
        {
            Key = DefaultKey;
            loadedKey = null;
            dirty = false;
        }

        static readonly HashSet<string> found = new HashSet<string>(), fresh = new HashSet<string>(), unlocked = new HashSet<string>();
        static readonly Dictionary<string, int> marks = new Dictionary<string, int>();
        static readonly HashSet<string> known = new HashSet<string>(), paid = new HashSet<string>(), clearedEver = new HashSet<string>();
        static readonly Dictionary<string, int> tally = new Dictionary<string, int>();
        // Counts went up since the file was last written. That alone is no reason to write it.
        static bool uncounted;
        static GameConfig config;
        static string loadedKey;
        static int nextItem = 1;
        static bool dirty;

        // The machines the player has built. A new profile has the starting machines of the game.
        public static readonly List<Loadout> Presets = new List<Loadout>();
        // Every part the player owns.
        public static readonly List<Item> Items = new List<Item>();
        public static int Beats { get; private set; }
        // Which machine is on the turntable.
        public static int Current { get; private set; }
        public static Loadout Machine => Presets[Mathf.Clamp(Current, 0, Presets.Count - 1)];
        // When the file was last written, in seconds since the game started. The mark on screen shows it.
        public static float SavedAt { get; private set; } = -99f;
        // Set while the Autosave object exists. Without it, as in the editor outside Play, Save writes at once.
        public static bool Deferred;

        static string PathFor(string key) => Path.Combine(Application.persistentDataPath, key + ".json");

        // ---- Loading ----

        // Loads the stored profile, or starts a new one. Does nothing when it is loaded already.
        public static void Load(GameConfig gameConfig)
        {
            if (loadedKey == Key && config == gameConfig && Presets.Count > 0) return;
            // Another profile was open: what it still owes the disk is written first.
            Flush();
            config = gameConfig;
            loadedKey = Key;
            found.Clear();
            fresh.Clear();
            unlocked.Clear();
            marks.Clear();
            known.Clear();
            paid.Clear();
            clearedEver.Clear();
            tally.Clear();
            uncounted = false;
            Presets.Clear();
            Items.Clear();
            Beats = Current = 0;
            nextItem = 1;

            var saved = Read(out bool imported);
            int version = saved != null ? saved.version : Version;
            if (saved != null)
            {
                Beats = saved.beats;
                nextItem = Mathf.Max(1, saved.nextItem);
                foreach (string name in saved.found)
                    // A profile from before frames were bought had every frame.
                    if (version >= 2 || Named(config.frames, name) == null) found.Add(name);
                foreach (string name in saved.fresh) fresh.Add(name);
                foreach (string name in saved.unlocked) unlocked.Add(name);
                for (int i = 0; i < saved.markedFrames.Count && i < saved.marks.Count; i++) marks[saved.markedFrames[i]] = saved.marks[i];
                foreach (string key in saved.known) known.Add(key);
                foreach (string key in saved.paid) paid.Add(key);
                foreach (string name in saved.cleared) clearedEver.Add(name);
                for (int i = 0; i < saved.tallied.Count && i < saved.tallies.Count; i++) tally[saved.tallied[i]] = saved.tallies[i];
                foreach (var item in saved.items)
                {
                    // An item whose module no longer exists is gone. One whose ability is gone keeps its level.
                    item.def = Named(config.modules, item.part);
                    if (item.def == null) continue;
                    item.ability = Named(config.perks, item.perk);
                    if (item.ability == null) item.rarity = Rarity.None;
                    Items.Add(item);
                    nextItem = Mathf.Max(nextItem, item.id + 1);
                }
                // Before parts were items, the player simply had every kind that was found.
                if (version < 3)
                    foreach (var module in config.modules)
                        if (module != null && found.Contains(module.name)) Add(module, Rarity.None, null, isNew: false);
            }

            // What every player has from the start: the frames that cost nothing, the parts listed for
            // it, and everything the starting machines are made of. A kind of part the player never had
            // is handed over as one plain item; that also covers parts added to the game later.
            foreach (var frame in config.frames)
                if (frame != null && frame.price <= 0) found.Add(frame.name);
            if (config.startingParts != null)
                foreach (var part in config.startingParts) Start(part);
            foreach (var loadout in config.loadouts)
            {
                Start(loadout.frame);
                Start(loadout.reactor);
                Start(loadout.drive);
                foreach (var entry in loadout.modules) Start(entry.module);
            }

            if (saved != null && version >= 2)
            {
                var byId = new Dictionary<int, Item>();
                foreach (var item in Items) byId[item.id] = item;
                foreach (var machine in saved.machines)
                {
                    var loadout = FromSaved(machine, byId, version);
                    if (loadout != null && Presets.Count < MaxPresets) Presets.Add(loadout);
                }
                Current = Mathf.Clamp(saved.current, 0, Mathf.Max(0, Presets.Count - 1));
            }
            // A new profile, or one that lost its machines: the starting machines.
            if (Presets.Count == 0)
                foreach (var loadout in config.loadouts)
                    if (Presets.Count < MaxPresets) Presets.Add(Equip(Copy(loadout, loadout.displayName)));

            // A profile that was new, older or stored in the old place is written in today's form at once.
            if (saved == null || version < Version || imported)
            {
                dirty = true;
                Flush();
                if (imported) PlayerPrefs.DeleteKey(Key);
            }
        }

        // Reads the file, or its backup when the file cannot be read, or the place profiles were kept
        // in before they were files.
        static Saved Read(out bool imported)
        {
            imported = false;
            string path = PathFor(Key);
            foreach (string candidate in new[] { path, path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    var saved = JsonUtility.FromJson<Saved>(File.ReadAllText(candidate));
                    if (saved != null) return saved;
                }
                catch (Exception error)
                {
                    Debug.LogWarning($"Pulse: could not read {candidate}: {error.Message}");
                }
            }
            if (!PlayerPrefs.HasKey(Key)) return null;
            imported = true;
            return JsonUtility.FromJson<Saved>(PlayerPrefs.GetString(Key));
        }

        // A part of the starting set. A module the player never had becomes a plain item.
        static void Start(UnityEngine.Object part)
        {
            if (part == null || !found.Add(part.name)) return;
            if (part is ModuleDef module) Add(module, Rarity.None, null, isNew: false);
        }

        // Throws the stored profile away and starts a new one.
        public static void Reset(GameConfig gameConfig)
        {
            Erase();
            Load(gameConfig);
        }

        // Reads the stored profile again, whatever is in memory. For the editor's menu, which may be
        // used long after the game last ran, or after tests that pointed Key somewhere else.
        public static void Reload(GameConfig gameConfig)
        {
            Flush();
            loadedKey = null;
            Load(gameConfig);
        }

        // Deletes what is stored under the current name, and forgets what is loaded.
        public static void Erase()
        {
            string path = PathFor(Key);
            foreach (string file in new[] { path, path + ".bak", path + ".tmp" })
                if (File.Exists(file)) File.Delete(file);
            PlayerPrefs.DeleteKey(Key);
            if (loadedKey == Key) loadedKey = null;
            dirty = false;
        }

        // ---- Saving ----

        // Notes that something changed. The file is written by Flush.
        public static void Save()
        {
            dirty = true;
            if (!Deferred) Flush();
        }

        // Writes the profile if anything changed since the last time.
        public static void Flush()
        {
            if (!dirty || config == null || loadedKey == null) return;
            dirty = uncounted = false;
            var saved = new Saved { version = Version, beats = Beats, current = Current, nextItem = nextItem };
            saved.known.AddRange(known);
            saved.paid.AddRange(paid);
            saved.cleared.AddRange(clearedEver);
            foreach (var pair in tally)
            {
                saved.tallied.Add(pair.Key);
                saved.tallies.Add(pair.Value);
            }
            saved.found.AddRange(found);
            saved.fresh.AddRange(fresh);
            saved.unlocked.AddRange(unlocked);
            saved.items.AddRange(Items);
            foreach (var pair in marks)
            {
                saved.markedFrames.Add(pair.Key);
                saved.marks.Add(pair.Value);
            }
            foreach (var loadout in Presets)
            {
                var machine = new SavedMachine
                {
                    name = loadout.displayName, frame = loadout.frame.name, reactor = loadout.reactor.name, drive = loadout.drive.name,
                };
                foreach (var entry in loadout.modules)
                {
                    if (entry.module == null) continue;
                    machine.slots.Add(entry.slot);
                    machine.modules.Add(entry.module.name);
                    machine.items.Add(entry.item != null ? entry.item.id : 0);
                }
                saved.machines.Add(machine);
            }

            string path = PathFor(loadedKey), fresher = path + ".tmp";
            try
            {
                File.WriteAllText(fresher, JsonUtility.ToJson(saved, true));
                if (File.Exists(path)) File.Replace(fresher, path, path + ".bak");
                else File.Move(fresher, path);
                SavedAt = Time.realtimeSinceStartup;
            }
            catch (Exception error)
            {
                // It is tried again the next time round.
                dirty = true;
                Debug.LogWarning($"Pulse: could not save to {path}: {error.Message}");
            }
        }

        // ---- Beats ----

        public static void Earn(int beats)
        {
            if (beats <= 0) return;
            Beats += beats;
            Save();
        }

        // Takes Beats away, if there are enough. Returns false and takes nothing when there are not.
        static bool Spend(int beats)
        {
            if (beats < 0 || beats > Beats) return false;
            Beats -= beats;
            return true;
        }

        // ---- The Codex: what the player has met, and what the Archivist has paid for ----

        public static bool Knows(string key) => known.Contains(key);

        public static bool IsPaid(string key) => paid.Contains(key);

        // How often something has happened, over all runs.
        public static int Count(string key) => tally.TryGetValue(key, out int count) ? count : 0;

        // Writes down that something happened once more. The first time is saved at once. After
        // that only its count goes up, and a count is written with whatever is saved next, or when
        // the run ends: a fight must not write the file once a second.
        public static void Note(string key)
        {
            if (string.IsNullOrEmpty(key) || config == null) return;
            tally[key] = Count(key) + 1;
            if (known.Add(key)) Save();
            else uncounted = true;
        }

        // Called when a run ends: the counts of that run are written.
        public static void Settle()
        {
            if (uncounted) Save();
        }

        // The Archivist pays for something it now knows. Once.
        public static bool Pay(string key, int reward)
        {
            if (!known.Contains(key) || !paid.Add(key)) return false;
            Beats += Mathf.Max(0, reward);
            Save();
            return true;
        }

        // Whether a dungeon has been cleared in any run. Some Echoes only join the pool then.
        public static bool HasCleared(DungeonDef dungeon) => dungeon != null && clearedEver.Contains(dungeon.name);

        // Returns true the first time a dungeon is cleared.
        public static bool NoteCleared(DungeonDef dungeon)
        {
            if (dungeon == null || !clearedEver.Add(dungeon.name)) return false;
            Save();
            return true;
        }

        // ---- Kinds of parts the player has come across ----

        public static bool IsFound(UnityEngine.Object part) => part != null && found.Contains(part.name);

        // Fresh: found, but not looked at in the Archive yet.
        public static bool IsFresh(UnityEngine.Object part) => part != null && fresh.Contains(part.name);

        // Hands the player a kind of part for the first time: a dungeon gives up what it kept. A
        // module arrives as a plain item. Returns false when the player had that kind already.
        public static bool Find(UnityEngine.Object part)
        {
            if (part == null || found.Contains(part.name)) return false;
            if (part is ModuleDef module) Add(module, Rarity.None, null, isNew: true);
            else
            {
                found.Add(part.name);
                fresh.Add(part.name);
            }
            Save();
            return true;
        }

        public static void Seen(UnityEngine.Object part)
        {
            if (part != null && fresh.Remove(part.name)) Save();
        }

        // ---- Items: the parts themselves ----

        static Item Add(ModuleDef module, Rarity rarity, PerkDef perk, bool isNew)
        {
            var item = new Item
            {
                id = nextItem++, part = module.name, def = module, level = 1, fresh = isNew,
                rarity = perk != null ? rarity : Rarity.None, ability = perk, perk = perk != null ? perk.name : "",
            };
            Items.Add(item);
            if (found.Add(module.name) && isNew) fresh.Add(module.name);
            return item;
        }

        // Gives the player a new part of a kind, with an ability or without.
        public static Item Grant(ModuleDef module, Rarity rarity = Rarity.None, PerkDef perk = null)
        {
            if (module == null) return null;
            var item = Add(module, rarity, perk, isNew: true);
            Save();
            return item;
        }

        // Takes a part an enemy dropped, as Tuning.Roll made it.
        public static Item Keep(Item dropped)
        {
            if (dropped == null || dropped.def == null) return null;
            dropped.id = nextItem++;
            dropped.fresh = true;
            Items.Add(dropped);
            if (found.Add(dropped.part)) fresh.Add(dropped.part);
            Save();
            return dropped;
        }

        public static void Seen(Item item)
        {
            if (item == null || !item.fresh) return;
            item.fresh = false;
            Save();
        }

        // Whether an item sits on one of the player's machines.
        public static bool IsFitted(Item item)
        {
            foreach (var loadout in Presets)
                foreach (var entry in loadout.modules)
                    if (entry.item == item) return true;
            return false;
        }

        // Raises an item by one level, for Beats.
        public static bool Upgrade(Item item)
        {
            if (item == null || !Items.Contains(item) || item.level >= Tuning.MaxLevel || !Spend(Tuning.UpgradeCost(item))) return false;
            item.level++;
            Save();
            return true;
        }

        // Breaks an item up for Beats. One that sits on a machine has to come off first.
        public static bool Scrap(Item item)
        {
            if (item == null || IsFitted(item) || !Items.Remove(item)) return false;
            Beats += Tuning.ScrapValue(item);
            Save();
            return true;
        }

        // ---- Rooms of the Roundhouse ----

        public static bool IsUnlocked(string room) => unlocked.Contains(room);

        public static bool Unlock(string room, int cost)
        {
            if (unlocked.Contains(room) || !Spend(cost)) return false;
            unlocked.Add(room);
            Save();
            return true;
        }

        // Shuts a room again and gives nothing back. Nothing in the game does this: it is there so
        // that buying a room open can be tried once more.
        public static bool Lock(string room)
        {
            if (!unlocked.Remove(room)) return false;
            Save();
            return true;
        }

        // ---- Frames: bought and upgraded at the Smelter ----

        // Which Mark of a frame the player has: 0 is Mark I.
        public static int MarkOf(FrameDef frame) => frame != null && marks.TryGetValue(frame.name, out int mark) ? mark : 0;

        // What the next step for a frame costs: its price while it is not owned, then the next Mark.
        // Zero: there is nothing more to buy.
        public static int NextCost(FrameDef frame)
        {
            if (!IsFound(frame)) return frame.price;
            int mark = MarkOf(frame);
            return mark < frame.marks.Length ? frame.marks[mark].cost : 0;
        }

        public static bool Buy(FrameDef frame)
        {
            if (frame == null || IsFound(frame) || !Spend(frame.price)) return false;
            found.Add(frame.name);
            Save();
            return true;
        }

        public static bool Upgrade(FrameDef frame)
        {
            if (!IsFound(frame)) return false;
            int mark = MarkOf(frame);
            if (mark >= frame.marks.Length || !Spend(frame.marks[mark].cost)) return false;
            marks[frame.name] = mark + 1;
            Save();
            return true;
        }

        // ---- Machines ----

        public static void Take(int index)
        {
            Current = Mathf.Clamp(index, 0, Presets.Count - 1);
            Save();
        }

        // A new machine that starts as a copy of another. Null when the list is full.
        public static Loadout AddPreset(Loadout like)
        {
            if (Presets.Count >= MaxPresets) return null;
            int number = Presets.Count + 1;
            string name = "Machine " + number;
            while (Presets.Exists(p => p.displayName == name)) name = "Machine " + ++number;
            var loadout = Copy(like, name);
            Presets.Add(loadout);
            Save();
            return loadout;
        }

        // Puts an edited machine in the place of a stored one.
        public static void Replace(int index, Loadout edited)
        {
            if (index < 0 || index >= Presets.Count) return;
            Presets[index] = Copy(edited, Presets[index].displayName);
            Save();
        }

        // The last machine cannot be removed.
        public static bool RemovePreset(int index)
        {
            if (Presets.Count <= 1 || index < 0 || index >= Presets.Count) return false;
            Presets.RemoveAt(index);
            if (Current >= index) Current = Mathf.Max(0, Current - 1);
            Save();
            return true;
        }

        public static Loadout Copy(Loadout source, string name)
        {
            var copy = ScriptableObject.CreateInstance<Loadout>();
            copy.name = copy.displayName = name;
            copy.frame = source.frame;
            copy.reactor = source.reactor;
            copy.drive = source.drive;
            copy.modules = (Loadout.Entry[])source.modules.Clone();
            return copy;
        }

        // Gives every slot of a machine that names only a kind of module one of the player's items
        // of that kind: the best one that is not on the machine already.
        static Loadout Equip(Loadout loadout)
        {
            for (int i = 0; i < loadout.modules.Length; i++)
            {
                var entry = loadout.modules[i];
                if (entry.module == null || entry.item != null) continue;
                entry.item = Spare(loadout, entry.module);
                loadout.modules[i] = entry;
            }
            return loadout;
        }

        // The player's best item of a kind that is not on that machine yet, or null.
        public static Item Spare(Loadout loadout, ModuleDef module)
        {
            Item best = null;
            foreach (var item in Items)
            {
                if (item.def != module || Array.Exists(loadout.modules, e => e.item == item)) continue;
                if (best == null || item.level * 10 + (int)item.rarity > best.level * 10 + (int)best.rarity) best = item;
            }
            return best;
        }

        // A stored machine is only read back when the player still has what it stands on. A slot
        // whose item is gone gets another of the same kind, or stays empty.
        static Loadout FromSaved(SavedMachine machine, Dictionary<int, Item> byId, int version)
        {
            var frame = Named(config.frames, machine.frame);
            var reactor = Named(config.reactors, machine.reactor);
            var drive = Named(config.drives, machine.drive);
            if (frame == null || reactor == null || drive == null || !found.Contains(frame.name)) return null;
            var loadout = ScriptableObject.CreateInstance<Loadout>();
            loadout.name = loadout.displayName = machine.name;
            loadout.frame = frame;
            loadout.reactor = reactor;
            loadout.drive = drive;
            var entries = new List<Loadout.Entry>();
            for (int i = 0; i < machine.slots.Count && i < machine.modules.Count; i++)
            {
                var module = Named(config.modules, machine.modules[i]);
                if (module == null) continue;
                Item item = null;
                if (i < machine.items.Count && byId.TryGetValue(machine.items[i], out var stored) && stored.def == module) item = stored;
                entries.Add(new Loadout.Entry { slot = version < 3 ? Renamed(machine.slots[i]) : machine.slots[i], module = module, item = item });
            }
            loadout.modules = entries.ToArray();
            Equip(loadout);
            // No item of that kind left: the slot is empty.
            loadout.modules = Array.FindAll(loadout.modules, e => e.item != null);
            return loadout;
        }

        // The names slots had before bays became systems and support slots became armor.
        public static string Renamed(string slot) => slot switch
        {
            "bay1" => "system1",
            "bay2" => "system2",
            "supportA" => "armor1",
            "supportB" => "armor2",
            "supportC" => "armor3",
            _ => slot,
        };

        static T Named<T>(T[] all, string name) where T : UnityEngine.Object
        {
            if (all != null && !string.IsNullOrEmpty(name))
                foreach (var one in all)
                    if (one != null && one.name == name) return one;
            return null;
        }
    }

    // The rhythm of the Pulse. Everything that is a piece of it throbs in this one shared rhythm:
    // every Beat on the ground and the counter that shows how many the player has.
    public static class Heart
    {
        const float PerSecond = 1.1f;

        // From 0 to 1: a strong thump followed by a weaker one, then rest.
        public static float Beat(float time)
        {
            float phase = Mathf.Repeat(time * PerSecond, 1f);
            return Mathf.Max(Thump(phase, 0.08f), 0.55f * Thump(phase, 0.3f));
        }

        static float Thump(float phase, float at) => Mathf.Max(0f, 1f - Mathf.Abs(phase - at) / 0.09f);
    }
}
