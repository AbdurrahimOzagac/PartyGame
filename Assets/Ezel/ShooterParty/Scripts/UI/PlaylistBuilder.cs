using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // The lobby's playlist panel. Everyone sees the room's playlist. The leader picks a preset, or opens the builder
    // (add, remove, reorder, shuffle, name) and sends the result; the server validates it again (SessionFlow.SetPlaylist).
    public class PlaylistBuilder : MonoBehaviour
    {
        [System.Serializable]
        public class Row
        {
            public GameObject root;
            public TMP_Text index, label;
            public Button up, down, remove;
        }

        public TMP_Text title, countLabel, errorLabel;
        public TMP_InputField nameField;
        public Row[] rows = new Row[PlaylistRules.MaxGames];
        public GameObject presetBar, editBar;
        public Button[] presetButtons = new Button[0];   // catalog.presets order
        public Button customButton;
        public Button[] addButtons = new Button[0];
        public string[] addIds = new string[0];           // the mini-game each add button adds
        public Button shuffleButton, applyButton, cancelButton;
        public Color presetIdle = new Color(1f, 1f, 1f, 0.12f), presetPicked = new Color32(0x00, 0xB3, 0xA4, 0xFF);

        public bool IsEditing { get; private set; }
        public string Error { get; private set; }
        public bool CanEdit { get; set; }   // LobbyView sets it from the local seat (leader only)

        readonly List<string> draft = new List<string>();
        readonly List<string> shown = new List<string>();
        string shownKey;

        static SessionManager Session => SessionManager.Instance;

        void Awake()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                int k = i;
                rows[i].up.onClick.AddListener(() => Move(k, -1));
                rows[i].down.onClick.AddListener(() => Move(k, 1));
                rows[i].remove.onClick.AddListener(() => RemoveAt(k));
            }
            for (int i = 0; i < addButtons.Length; i++) { var id = addIds[i]; addButtons[i].onClick.AddListener(() => Add(id)); }
            for (int i = 0; i < presetButtons.Length; i++) { int k = i; presetButtons[i].onClick.AddListener(() => UsePreset(k)); }
            customButton.onClick.AddListener(Open);
            shuffleButton.onClick.AddListener(Shuffle);
            applyButton.onClick.AddListener(() => Apply());
            cancelButton.onClick.AddListener(Cancel);
            nameField.characterLimit = PlaylistRules.MaxNameLength;
            nameField.onValueChanged.AddListener(_ => SetError(null));
        }

        public void UsePreset(int i)
        {
            var s = Session;
            if (!s || !s.catalog || i < 0 || i >= s.catalog.presets.Count) return;
            var p = s.catalog.presets[i];
            s.RequestPlaylist(p.name, p.games);
        }

        // Start a draft from the room's playlist. A preset's name is not kept: a custom list gets its own name.
        public void Open()
        {
            var s = Session;
            draft.Clear();
            if (s) for (int i = 0; i < s.PlaylistIds.Count; i++) draft.Add(s.PlaylistIds[i].ToString());
            var current = s ? s.PlaylistName.Value.ToString() : "";
            nameField.text = PresetIndex(current) >= 0 || current == "" ? "My playlist" : current;
            IsEditing = true;
            SetError(null);
            Redraw(true);
        }

        public void Clear() { draft.Clear(); Redraw(true); }
        public void SetName(string name) => nameField.text = name;

        public void Add(string id)
        {
            if (draft.Count >= PlaylistRules.MaxGames) { SetError($"A playlist can have at most {PlaylistRules.MaxGames} games"); return; }
            draft.Add(id);
            Redraw(true);
        }

        public void RemoveAt(int i)
        {
            if (i < 0 || i >= draft.Count) return;
            draft.RemoveAt(i);
            Redraw(true);
        }

        public void Move(int i, int by)
        {
            int j = i + by;
            if (i < 0 || j < 0 || i >= draft.Count || j >= draft.Count) return;
            (draft[i], draft[j]) = (draft[j], draft[i]);
            Redraw(true);
        }

        public void Shuffle()
        {
            for (int i = draft.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (draft[i], draft[j]) = (draft[j], draft[i]);
            }
            Redraw(true);
        }

        // Checked here first for an instant message, then sent; the server has the final say.
        public bool Apply()
        {
            var s = Session;
            if (!s || !s.catalog) return false;
            if (!PlaylistRules.TryValidate(nameField.text, draft, s.catalog.Has, out var error)) { SetError(error); return false; }
            s.RequestPlaylist(nameField.text.Trim(), draft);
            IsEditing = false;
            SetError(null);
            Redraw(true);
            return true;
        }

        public void Cancel()
        {
            IsEditing = false;
            SetError(null);
            Redraw(true);
        }

        int PresetIndex(string name)
        {
            var s = Session;
            return s && s.catalog ? s.catalog.presets.FindIndex(p => p.name == name) : -1;
        }

        void SetError(string e)
        {
            Error = e;
            if (errorLabel) errorLabel.text = e ?? "";
            if (countLabel) countLabel.gameObject.SetActive(string.IsNullOrEmpty(e));   // the error borrows the count's line
        }

        void Update()
        {
            if (IsEditing && !CanEdit) Cancel();   // leadership moved on
            Redraw(false);
        }

        void Redraw(bool force)
        {
            var s = Session;
            string name = s ? s.PlaylistName.Value.ToString() : "";
            shown.Clear();
            if (IsEditing) shown.AddRange(draft);
            else if (s) for (int i = 0; i < s.PlaylistIds.Count; i++) shown.Add(s.PlaylistIds[i].ToString());
            var key = $"{IsEditing}|{CanEdit}|{name}|{string.Join(",", shown)}";
            if (!force && key == shownKey) return;
            shownKey = key;

            title.text = name == "" ? "No playlist yet" : name;
            title.gameObject.SetActive(!IsEditing);
            nameField.gameObject.SetActive(IsEditing);
            presetBar.SetActive(CanEdit && !IsEditing);
            editBar.SetActive(IsEditing);
            countLabel.text = IsEditing ? $"{shown.Count} of {PlaylistRules.MaxGames} games" : shown.Count == 1 ? "1 game" : $"{shown.Count} games";
            int picked = PresetIndex(name);
            for (int i = 0; i < presetButtons.Length; i++) presetButtons[i].targetGraphic.color = i == picked ? presetPicked : presetIdle;
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                bool on = i < shown.Count;
                r.root.SetActive(on);
                if (!on) continue;
                var game = s && s.catalog ? s.catalog.Find(shown[i]) : null;
                r.index.text = (i + 1).ToString();
                r.label.text = game ? game.displayName : shown[i];
                r.up.gameObject.SetActive(IsEditing);
                r.down.gameObject.SetActive(IsEditing);
                r.remove.gameObject.SetActive(IsEditing);
                r.up.interactable = i > 0;
                r.down.interactable = i < shown.Count - 1;
            }
            foreach (var b in addButtons) b.interactable = shown.Count < PlaylistRules.MaxGames;
        }
    }
}
