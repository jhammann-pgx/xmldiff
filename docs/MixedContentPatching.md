# Mixed Content: Diff und Patch

Wie XmlDiff/XmlPatch Änderungen an Dokumenten mit Mixed Content überträgt — und wie ein
Diffgram auch dann noch angewendet werden kann, wenn das Zieldokument eine andere
Knotenreihenfolge hat als das Dokument, aus dem das Diffgram erzeugt wurde.

## Das Grundproblem

Ein XDL-Diffgram adressiert Knoten **ausschließlich über ihre Position** im Elternknoten:
`match="2"` heißt „zweites Kind". Kein Typ, kein Name.

Bei datenzentriertem XML ist das unkritisch. Bei Mixed Content — Text und Inline-Elemente
gemischt in einem Absatz — nicht, denn dort ist die Reihenfolge Inhalt:

```xml
<p>Hallo <b id="1">Welt</b></p>      <!-- <b> ist Kind 2 -->
<p><b id="1">World</b> hello</p>     <!-- <b> ist Kind 1 -->
```

Beide Absätze enthalten dasselbe, aber `match="2"` trifft im einen Fall das `<b>`-Element,
im anderen den Textknoten. Ein Diffgram, das für das erste Dokument erzeugt wurde, patcht
im zweiten den falschen Knoten — oder scheitert mit einer irreführenden Meldung.

Deshalb existiert im Diffgram der `srcDocHash`: Das Format ist per Design nur auf **exakt**
das Dokument anwendbar, aus dem es erzeugt wurde. Wer ein Diffgram auf ein abweichendes
Dokument anwendet (z. B. eine Übersetzung), verlässt diese Garantie.

## Der Ablauf

```
   doc1 (alt) ──┐
                ├─► XmlDiff.Compare() ─► Diffgram (XDL)
   doc2 (neu) ──┘                            │
                                             ▼
   Zieldokument ──────────────────► XmlPatch.Patch() ─► gepatchtes Dokument
```

Das Diffgram beschreibt nur den Unterschied doc1 → doc2. Wird es auf ein **drittes**
Dokument angewendet, das von doc1 abweicht (Reihenfolge, übersetzte Texte), braucht der
Patcher Zusatzinformationen, um die gemeinten Knoten wiederzufinden. Genau dafür sind die
beiden Schalter unten da.

## Die Schalter

| Schalter | Klasse | Default | Zweck |
|---|---|---|---|
| `EmitMatchValidation` | `XmlDiff` | `false` | Schreibt Erwartungs-Metadaten ins Diffgram |
| `EnableMatchReanchoring` | `XmlPatch` | `false` | Sucht Knoten inhaltsbasiert statt nur positionsbasiert |
| `IgnoreSrcValidation` | `XmlPatch` | `false` | Überspringt die `srcDocHash`- und Inhalts-Prüfung |

Ohne diese Schalter verhält sich die Bibliothek wie das Original von Microsoft. Der
Diffgram-Output ist ohne `EmitMatchValidation` byteidentisch zu früher.

```csharp
// Diff-Seite
var diff = new XmlDiff(XmlDiffOptions.None) { EmitMatchValidation = true };
diff.Compare(doc1, doc2, diffgramWriter);

// Patch-Seite
var patch = new XmlPatch {
    EnableMatchReanchoring = true,
    IgnoreSrcValidation    = true   // nötig, wenn das Zieldokument inhaltlich abweicht
};
patch.Patch(zielDokument, diffgramReader);
```

## Beispiel

An jedem Durchlauf sind **drei** Dokumente beteiligt (siehe „Der Ablauf" oben): zwei bilden
den Diff, das dritte wird gepatcht.

**① doc1 und ② doc2 — die beiden Diff-Eingaben** (nur das Attribut `id` ändert sich):

```xml
doc1: <p>Hallo <b id="1">Welt</b></p>
doc2: <p>Hallo <b id="2">Welt</b></p>
```

**Diffgram aus `XmlDiff.Compare(doc1, doc2)` mit `EmitMatchValidation = true`:**

```xml
<xd:xmldiff version="1.0" srcDocHash="5560047454017179883" options="None" fragments="no"
            xmlns:xd="http://schemas.microsoft.com/xmltools/2002/xmldiff">
  <xd:node match="1" matchType="1" matchName="p" matchHash="3603834883815440618">
    <xd:node match="2" matchType="1" matchName="b" matchHash="5894552796600918663">
      <xd:change match="@id">2</xd:change>
    </xd:node>
  </xd:node>
</xd:xmldiff>
```

Neu sind die drei `match*`-Attribute. Sie beschreiben, **welchen Knoten** die Operation
erwartet:

| Attribut | Bedeutung |
|---|---|
| `matchType` | `XmlNodeType` als Zahl (`1` = Element, `3` = Text, `4` = CDATA, `8` = Kommentar) |
| `matchName` | LocalName bei Elementen, Name bei PI/EntityReference/DocType |
| `matchHash` | Inhalts-Hash des Knotens inkl. Teilbaum (dieselbe Hash-Funktion wie `srcDocHash`) |

**③ Das Zieldokument — ein von doc1 abweichendes drittes Dokument** (`<b>` steht vorn, Text
ist bereits übersetzt). `srcDocHash` und Inhalts-Hashes passen hier nicht mehr, deshalb
`IgnoreSrcValidation = true`:

```xml
Ziel:     <p><b id="1">World</b> hello</p>
Ergebnis: <p><b id="2">World</b> hello</p>
```

Das `match="2"` zeigt im Zieldokument auf den Textknoten `" hello"` (in doc1 war Kind 2 das
`<b>`). Über `matchType="1"`/`matchName="b"` findet der Patcher das gemeinte Element an
Position 1 und ändert dort das Attribut. Der übersetzte Text bleibt unangetastet und wird
**nicht verschoben**.

Zum Vergleich dasselbe Zieldokument ohne die Schalter:

```
ohne EmitMatchValidation:  Fehler: "… matched a Text node which cannot contain children …"
mit Metadaten, ohne Reanchoring: Fehler: "… matched a node of type Text, but the diffgram
                                  was generated for a node of type Element …"
mit beidem:                <p><b id="2">World</b> hello</p>   ✔
```

Der normale Fall — Diffgram auf das unveränderte doc1 anwenden — liefert wie mit der
Originalbibliothek `<p>Hallo <b id="2">Welt</b></p>`. Re-Anchoring greift dort nie ein, weil
die Positionen und Hashes passen.

## Wie der Patcher einen Knoten auflöst

Für jede Operation mit `match`-Pfad, in dieser Reihenfolge:

1. **Position auflösen** — `match="2"` → zweites Kind. (Klassisches Verhalten.)
2. **Hash prüfen** — stimmt `matchHash` mit dem Knoten an der Position überein, ist es der
   richtige Knoten. Fertig.
3. **Re-Anchoring per Hash** (nur mit `EnableMatchReanchoring`) — sonst werden die
   Geschwister nach dem Knoten mit dem erwarteten Hash durchsucht. Bei mehreren gleichen
   Treffern gewinnt der positionsnächste (inhaltsgleich = austauschbar).
4. **Re-Anchoring per Typ + Name** (nur mit `EnableMatchReanchoring` **und**
   `IgnoreSrcValidation`) — ein inhaltlich geänderter Knoten (übersetzter Text!) ist per
   Hash nicht auffindbar. Passt der Positionsknoten nicht einmal zu `matchType`/`matchName`,
   wird der nächstgelegene Geschwisterknoten genommen, der passt.
5. **Validierung** — findet sich kein Kandidat, greifen die Prüfungen, sofern das Diffgram
   Match-Metadaten trägt (also mit `EmitMatchValidation` erzeugt wurde): Typ und Name werden
   dann geprüft, der Inhalts-Hash nur zusätzlich ohne `IgnoreSrcValidation`. Passt etwas
   nicht, gibt es einen sprechenden Fehler statt eines falsch gepatchten Knotens.

`IgnoreSrcValidation` schaltet also nur die **Inhalts**-Prüfung ab („die Dokumente dürfen
sich unterscheiden"), nicht die Struktur-Prüfung: Ein Element bleibt ein Element, `<b>`
bleibt `<b>`.

Mehrfach-Matches (Intervalle wie `match="2-4"`) werden nur validiert, nie re-geankert.

## Platzierung neuer Knoten (`xd:add`)

Ein neuer Knoten hat im Zieldokument kein Gegenstück — er ist per Hash nicht auffindbar.
Für seine Position gibt es deshalb drei Regeln:

| Situation | Verhalten |
|---|---|
| Neuer Inhalt am **Ende** des Absatzes in doc2 | `anchorLast="yes"` im Diffgram → wird ans Elternende angehängt |
| Neuer Inhalt **mitten** im Absatz | relational: hinter dem Knoten der vorherigen Operation |
| Neuer Inhalt am **Anfang** | am Anfang des Elternknotens |

`anchorLast` wird nur mit `EmitMatchValidation` geschrieben und nur mit
`EnableMatchReanchoring` ausgewertet. Markiert wird die gesamte zusammenhängende Gruppe
neuer Knoten am Zielende — auch wenn sie aus mehreren Inline-Elementen und Text besteht.

Beispiel — auch hier sind es drei Dokumente:

```xml
① doc1:  <p><g id="1"/>alter text</p>          <!-- Diff-Quelle -->
② doc2:  <p><g id="2"/>neuer text<x>Neu</x></p>  <!-- Diff-Ziel: id + Text geändert, <x> neu -->
③ Ziel:  <p>uebersetzter text<g id="1"/></p>    <!-- wird gepatcht; <g> steht hinten, Text übersetzt -->
```

Diffgram aus `Compare(doc1, doc2)` (der äußere `<xd:xmldiff srcDocHash="8413154322066546735" …>`
ist der Übersicht halber weggelassen):

```xml
<xd:node match="1" matchType="1" matchName="p" matchHash="5684874897429377070">
  <xd:node match="1" matchType="1" matchName="g" matchHash="10618807583106684890">
    <xd:change match="@id">2</xd:change>
  </xd:node>
  <xd:change match="2" matchType="3" matchHash="12000377718301525272">neuer text</xd:change>
  <xd:add anchorLast="yes"><x>Neu</x></xd:add>
</xd:node>
```

Angewendet auf ③ (mit `EnableMatchReanchoring` + `IgnoreSrcValidation`) ergibt das
`<p>neuer text<g id="2" /><x>Neu</x></p>`: Die Attributänderung findet ihre Grafik trotz
vertauschter Position, und `<x>Neu</x>` landet am Absatzende statt hinter dem Textknoten.

## Fehlermeldungen

Alle Meldungen nennen den `match`-Pfad und was statt des Erwarteten gefunden wurde:

| Meldung (gekürzt) | Ursache |
|---|---|
| `… matched a {Typ} node which cannot contain children …` | Operation will in einen Text-/CDATA-/Kommentarknoten absteigen |
| `… matched a node of type X, but … generated for a node of type Y …` | `matchType` passt nicht |
| `… matched a node named 'x', but … named 'y' …` | `matchName` passt nicht |
| `… the content of the node(s) … differs …` | `matchHash` passt nicht (nur ohne `IgnoreSrcValidation`) |
| `… the xd:change operation … does not fit the matched {Typ} node …` | Payload der Change-Operation passt nicht zum Knotentyp |

Diese Prüfungen ersetzen Fälle, in denen früher still der falsche Knoten gepatcht oder eine
Änderung kommentarlos verworfen wurde. Sie zerfallen in zwei Gruppen:

- **Immer aktiv, auch bei alten Diffgrams ohne `match*`-Attribute:** die beiden strukturellen
  Prüfungen `… cannot contain children …` und `… does not fit the matched … node …`. Sie
  leiten sich allein aus dem Knotentyp am Zielort und der Art der Operation ab.
- **Nur bei Diffgrams mit Match-Metadaten** (also mit `EmitMatchValidation` erzeugt): die
  Typ-, Namens- und Inhalts-Mismatches. Ohne `match*`-Attribute gibt es nichts zu vergleichen,
  diese Meldungen können dann nicht auftreten. Der Inhalts-Hash wird zudem nur ohne
  `IgnoreSrcValidation` geprüft.

## Kompatibilität

- Alte Diffgrams (ohne `match*`-Attribute) funktionieren unverändert; Re-Anchoring bleibt
  dann wirkungslos.
- Neue Diffgrams sind für alte Patcher lesbar — unbekannte Attribute werden ignoriert.
- Änderungen an den Schaltern erfordern **neu erzeugte** Diffgrams, wenn `EmitMatchValidation`
  neu aktiviert wird.

## Grenzen

- **Zwei-Wege-Format für Drei-Wege-Problem.** Ein Diffgram beschreibt doc1 → doc2. Es auf ein
  drittes Dokument anzuwenden ist ein Merge — der ist ohne Policies nicht eindeutig lösbar.
  Die Regeln oben sind solche Policies.
- **Mehrdeutige Kandidaten.** Existieren mehrere Geschwister mit gleichem Typ und Namen,
  deren Inhalte alle abweichen (z. B. mehrere übersetzte `<b>` im selben Absatz), kann der
  Typ+Name-Fallback sie nicht unterscheiden — es entscheidet die Nähe zur Originalposition.
- **Geänderter *und* verschobener Knoten.** Hash findet ihn nicht, Typ+Name nur, wenn er
  eindeutig ist. Sonst greift die Validierung bzw. die Toleranz unter `IgnoreSrcValidation`.
- **Bestehende Knoten werden nie umsortiert.** Der Patcher ändert Werte und Attribute an Ort
  und Stelle. Weicht die Reihenfolge des Zieldokuments von doc2 ab, bleibt sie so.

Wenn eine Übersetzungseinheit ohnehin komplett neu übersetzt werden muss, ist es oft
robuster, ihren Inhalt auf Anwendungsebene direkt aus doc2 zu übernehmen, statt ihn
feingranular zu patchen.

## Wo im Code

| Datei | Inhalt |
|---|---|
| `src/XmlDiff/XmlDiff.cs` | `EmitMatchValidation` |
| `src/XmlDiff/DiffgramOperation.cs` | Schreiben von `matchType`/`matchName`/`matchHash`, `anchorLast` |
| `src/XmlDiff/XmlPatch.cs` | `EnableMatchReanchoring`, Auflösung, Validierung, Re-Anchoring |
| `src/XmlDiff/XmlPatchOperations.cs` | Anwenden der Operationen, Einfügeregeln |
| `src/XmlDiff/XmlPatchError.cs` | Fehlermeldungen |
| `src/UnitTests/UnitTest1.cs` | Tests, u. a. `Reanchoring*` und `MatchValidation*` |
