---@meta
-- =============================================================
--  AiComi / xLua  –  Type Stubs für VS Code IntelliSense
--  Basierend auf dekompilierten DLLs: XLua.Runtime.dll, AC.Lua.dll
--  Kein echter Lua-Code – nur Typ-Annotationen für den LSP
-- =============================================================


-- =============================================================
--  xlua  –  globales xLua-API
-- =============================================================

---@class xlua
---Ersetzt eine C#-Methode zur Laufzeit durch eine Lua-Funktion (Hotfix)
---@param cs any         Der C#-Typ z.B. CS.MeinNamespace.MeineKlasse
---@param field string   Methodenname als String, oder table mit mehreren Methoden
---@param func function  Die Ersatz-Lua-Funktion (nil zum Zurücksetzen)
xlua = {}
function xlua.hotfix(cs, field, func) end

---Macht private C#-Member eines Typs aus Lua zugänglich
---@param cs any
function xlua.private_accessible(cs) end

---Setzt einen Debug-Hook (wie debug.sethook, aber für xLua-Coroutines)
---@param hook function
---@param mask string
function xlua.sethook(hook, mask) end

---Gibt die xLua-Version zurück
---@return string
function xlua.version() end


-- =============================================================
--  typeof  –  xLua-Hilfsfunktion (Lua -> C# System.Type)
-- =============================================================

---Gibt den C# System.Type eines CS-Objekts oder einer Klasse zurück
---@param cs any
---@return userdata
function typeof(cs) end


-- =============================================================
--  CS  –  Brücke zu C#-Namespaces
-- =============================================================

---@class CS
CS = {}


-- =============================================================
--  CS.AC.Lua  –  AiComi eigene Lua-Infrastruktur (AC.Lua.dll)
-- =============================================================

---@class CS.AC.Lua
CS.AC = {}
CS.AC.Lua = {}

---@class AC_Lua_Logger
---@field Log      fun(text: string)              Schreibt eine Log-Zeile
---@field LogFormat fun(format: string, ...: any) Formatiertes Logging
CS.AC.Lua.Logger = {}

---Schreibt eine einfache Log-Zeile
---@param text string
function CS.AC.Lua.Logger.Log(text) end

---Formatiertes Logging (wie string.format)
---@param format string
---@param ... any
function CS.AC.Lua.Logger.LogFormat(format, ...) end


---@class AC_Lua_EventTable
---Statisches Event-Gedächtnis des Spiels. HashSet<int> mit allen bereits
---gefeuerten Event-IDs. Manipulation ändert den Spielzustand direkt.
---@field EventMemory userdata    HashSet<int> – alle gefeuerten Event-IDs
CS.AC.Lua.EventTable = {}

---Prüft ob ein Event bereits in der Spielhistorie vermerkt ist
---@param eventID integer
---@return boolean
function CS.AC.Lua.EventTable.ContainsMemory(eventID) end


---@class AC_Lua_Predicate
---Parameterloser Boolean-Delegate. Wird für Story-Bedingungen genutzt.
CS.AC.Lua.Predicate = {}

---Ruft den Predicate-Delegate auf
---@return boolean
function CS.AC.Lua.Predicate:Invoke() end


-- =============================================================
--  CS.AC  –  Haupt-Spielcode (Assembly-CSharp)
-- =============================================================

---@class AC_ParameterContainer
---Zentrale Klasse für Spielparameter und Story-Bedingungen.
---Hält _luaEnv (XLua.LuaEnv) und _predicates (StringKeyDictionary<Predicate>).
---@field _luaEnv       userdata                LuaEnv – die aktive Lua-Umgebung
---@field _predicates   userdata                StringKeyDictionary<Predicate> – Story-Bedingungen (key = ConditionFile-Name)
---@field _saveData     userdata                Save-spezifische Daten
---@field _globalData   userdata                Globale Spieldaten
---@field HumanData     userdata                Aktuelle NPC-Daten
CS.AC.ParameterContainer = {}

---Baut die Predicate-Delegates aus Lua-Condition-Dateien auf.
---Wird beim Laden eines Saves aufgerufen. Danach ist _luaEnv verfügbar.
function CS.AC.ParameterContainer:BuildConditionsFromLua() end

---Prüft eine benannte Bedingung mit Spieler- und NPC-Kontext.
---@param conditionName string   Name der Bedingung (= ConditionFile key in _predicates)
---@param player userdata        PlayerData
---@param npc userdata           NPCData
---@return boolean
function CS.AC.ParameterContainer:ValidateCondition(conditionName, player, npc) end

---Prüft eine Bedingung direkt ohne Kontext-Setup.
---@param conditionName string
---@return boolean
function CS.AC.ParameterContainer:ValidateConditionStraight(conditionName) end

---Setzt Lua-Parameter vor einer Bedingungsprüfung (Player, NPC, EventID etc.)
---@param player userdata
---@param npc userdata
---@param uniqueNPC userdata|nil
---@param eventID integer
function CS.AC.ParameterContainer:PrebuildLuaParameter(player, npc, uniqueNPC, eventID) end

---Sucht ein passendes Kommunikations-Event für den aktuellen Kontext.
---@param player userdata
---@param npc userdata
---@param content userdata      out EventScenarioContent
---@return boolean
function CS.AC.ParameterContainer:SearchCommunicationEvent(player, npc, content) end

---@class AC_EventScenarioContent
---Ein Event-Eintrag aus ContentProvider.EventScenarioContents.
---@field Type              integer    Event-Typ (0=Communication, 1=Heroine, 2=H, 4=Field, 10=Unique)
---@field ID                integer    Interne ID
---@field EventID           integer    Event-ID (für EventMemory)
---@field ConditionFile     string     Name der Lua-Condition-Datei (= key in _predicates)
---@field ScenarioFile      string     Name der Szenario-Datei (für ADV)
---@field Priority          integer    Priorität bei mehreren passenden Events
---@field Once              boolean    Nur einmal feuerbar
---@field _targetPersonality integer   Ziel-Persönlichkeitstyp
---@field _targetRelation   integer    Ziel-Beziehungsstufe
---@field _targetTimeZone   integer    Ziel-Tageszeit
---@field TableID           integer    Tabellen-ID
CS.AC.EventScenarioContent = {}

---Prüft ob die Persönlichkeit passt
---@param personality integer
---@return boolean
function CS.AC.EventScenarioContent:IsTargetPersonality(personality) end

---Prüft ob die Beziehungsstufe passt
---@param relation integer
---@return boolean
function CS.AC.EventScenarioContent:IsTargetRelation(relation) end

---Prüft ob die Tageszeit passt
---@param timeZone integer
---@return boolean
function CS.AC.EventScenarioContent:IsTargetTimeZone(timeZone) end

---@class AC_ContentProvider
---Hält alle Event-Szenario-Inhalte des Spiels.
---@field EventScenarioContents userdata  Dictionary/Array der EventScenarioContent
CS.AC.ContentProvider = {}

---@class AC_GenConfig
---xLua Codegen-Konfiguration. Listet welche C#-Typen für Lua exposed sind.
---@field LuaCallCSharp userdata  List<Type> – Typen die Lua aufrufen kann
---@field CSharpCallLua userdata  List<Type> – Delegates die C# nach Lua delegiert
---@field BlackList     userdata  List<List<string>> – ausgeschlossene Member
CS.AC.GenConfig = {}


-- =============================================================
--  CS.XLuaTest  –  xLua Demo/Runtime Klassen
-- =============================================================

---@class CS.XLuaTest
CS.XLuaTest = {}

---@class XLuaTest_LuaBehaviour
---MonoBehaviour das Lua-Scripts an GameObjects bindet.
---luaEnv ist STATISCH – geteilt von allen Instanzen im Spiel.
---@field luaEnv      userdata   LuaEnv – statische, globale Lua-Umgebung
---@field luaScript   userdata   TextAsset – das zugeordnete Lua-Script
---@field injections  userdata   Injection[] – Name->GameObject Mappings
---@field scriptEnv   userdata   LuaTable – Umgebungs-Table des Scripts
---@field luaStart    function   Wird von Unity Start() aufgerufen
---@field luaUpdate   function   Wird von Unity Update() aufgerufen
---@field luaOnDestroy function  Wird von Unity OnDestroy() aufgerufen
---@field lastGCTime  number
---@field GCInterval  number
CS.XLuaTest.LuaBehaviour = {}

---@class XLuaTest_Injection
---Datencontainer: bindet einen Namen an ein GameObject für Lua-Scripts
---@field name  string          Name des GameObjects
---@field value userdata        Das GameObject selbst
CS.XLuaTest.Injection = {}

---@class XLuaTest_Coroutine_Runner
---MonoBehaviour für Lua-gesteuerte Coroutines
CS.XLuaTest.Coroutine_Runner = {}
function CS.XLuaTest.Coroutine_Runner:StartCoroutine(generator) end
function CS.XLuaTest.Coroutine_Runner:StopCoroutine(coroutine) end

---@class XLuaTest_MessageBox
CS.XLuaTest.MessageBox = {}
function CS.XLuaTest.MessageBox.ShowAlertBox(message, title, callback) end
function CS.XLuaTest.MessageBox.ShowConfirmBox(message, title, callback) end


-- =============================================================
--  CS.XLua  –  xLua Runtime Internals
-- =============================================================

---@class CS.XLua
CS.XLua = {}

---@class XLua_HotfixDelegateBridge
---Direkte Hotfix-Bridge. Normalerweise über xlua.hotfix() genutzt,
---aber auch direkt verwendbar wenn man die ID kennt.
CS.XLua.HotfixDelegateBridge = {}

---Setzt einen Hotfix direkt per numerischer ID (aus hotfix_id_map)
---@param id integer
---@param func function|nil  nil entfernt den Hotfix
function CS.XLua.HotfixDelegateBridge.Set(id, func) end


-- =============================================================
--  CS.UnityEngine  –  Unity API (häufig genutzte Teile)
-- =============================================================

---@class CS.UnityEngine
CS.UnityEngine = {}

---@class UnityEngine_GameObject
---@field name      string
---@field tag       string
---@field layer     integer
---@field transform userdata
---@field activeSelf boolean
CS.UnityEngine.GameObject = {}

---Findet ein GameObject im Szenenbaum nach Name
---@param name string
---@return userdata
function CS.UnityEngine.GameObject.Find(name) end

---Erstellt ein neues leeres GameObject
---@param name? string
---@return userdata
function CS.UnityEngine.GameObject.new(name) end

---Gibt alle Root-GameObjects der aktiven Szene zurück
---@return userdata[]
function CS.UnityEngine.GameObject.FindObjectsOfType() end

---Fügt eine Komponente hinzu
---@param type userdata  typeof(CS.Namespace.Klasse)
---@return userdata
function CS.UnityEngine.GameObject:AddComponent(type) end

---Gibt eine Komponente zurück
---@param type string|userdata
---@return userdata
function CS.UnityEngine.GameObject:GetComponent(type) end

---Aktiviert/deaktiviert das GameObject
---@param active boolean
function CS.UnityEngine.GameObject:SetActive(active) end

---@class UnityEngine_Object
CS.UnityEngine.Object = {}

---Verhindert das Zerstören beim Szenenwechsel
---@param obj userdata
function CS.UnityEngine.Object.DontDestroyOnLoad(obj) end

---Zerstört ein GameObject oder eine Komponente
---@param obj userdata
---@param delay? number
function CS.UnityEngine.Object.Destroy(obj, delay) end

---WaitForSeconds für Coroutines
---@param seconds number
---@return userdata
function CS.UnityEngine.WaitForSeconds(seconds) end

---WaitForEndOfFrame für Coroutines
---@return userdata
function CS.UnityEngine.WaitForEndOfFrame() end

---@class UnityEngine_Debug
CS.UnityEngine.Debug = {}
---@param message any
function CS.UnityEngine.Debug.Log(message) end
---@param message any
function CS.UnityEngine.Debug.LogWarning(message) end
---@param message any
function CS.UnityEngine.Debug.LogError(message) end

---@class UnityEngine_SceneManagement
CS.UnityEngine.SceneManagement = {}
CS.UnityEngine.SceneManagement.SceneManager = {}
---@return userdata  aktive Szene
function CS.UnityEngine.SceneManagement.SceneManager.GetActiveScene() end

---@class UnityEngine_Time
CS.UnityEngine.Time = {}
---@type number  Sekunden seit Spielstart
CS.UnityEngine.Time.time = 0
---@type number  Delta-Zeit des letzten Frames
CS.UnityEngine.Time.deltaTime = 0
---@type number  Unscaled Delta-Zeit
CS.UnityEngine.Time.unscaledDeltaTime = 0

---@class UnityEngine_PlayerPrefs
CS.UnityEngine.PlayerPrefs = {}
function CS.UnityEngine.PlayerPrefs.GetInt(key, default_) end
function CS.UnityEngine.PlayerPrefs.SetInt(key, value) end
function CS.UnityEngine.PlayerPrefs.GetFloat(key, default_) end
function CS.UnityEngine.PlayerPrefs.SetFloat(key, value) end
function CS.UnityEngine.PlayerPrefs.GetString(key, default_) end
function CS.UnityEngine.PlayerPrefs.SetString(key, value) end
function CS.UnityEngine.PlayerPrefs.Save() end
function CS.UnityEngine.PlayerPrefs.DeleteKey(key) end

---@class UnityEngine_Resources
CS.UnityEngine.Resources = {}
function CS.UnityEngine.Resources.Load(path, type_) end
function CS.UnityEngine.Resources.FindObjectsOfTypeAll(type_) end

---@class UnityEngine_Screen
CS.UnityEngine.Screen = {}
---@type integer
CS.UnityEngine.Screen.width = 0
---@type integer
CS.UnityEngine.Screen.height = 0


-- =============================================================
--  CS.System  –  .NET Basics
-- =============================================================

---@class CS.System
CS.System = {}
CS.System.Collections = {}
CS.System.Collections.Generic = {}
CS.System.Reflection = {}
CS.System.Reflection.BindingFlags = {}
CS.System.Reflection.BindingFlags.Public = nil
CS.System.Reflection.BindingFlags.NonPublic = nil
CS.System.Reflection.BindingFlags.Instance = nil
CS.System.Reflection.BindingFlags.Static = nil

CS.System.Delegate = {}
function CS.System.Delegate.CreateDelegate(delegateType, obj, method) end