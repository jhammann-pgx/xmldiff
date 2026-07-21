# Diffgram und Patch — Format, Ablauf, Beispiele

Diese Datei erklärt, wie XmlDiff/XmlPatch arbeiten: welches Format ein Diffgram hat, wie die
einzelnen Operationen aussehen und warum das Format empfindlich auf Abweichungen im
Zieldokument reagiert. Am Ende steht ein Vorher/Nachher-Vergleich, was die Änderungen auf
diesem Branch daran ändern.

Alle Diffgrams und Patch-Ergebnisse in diesem Dokument sind **echte Ausgaben** der Bibliothek,
nicht handgeschriebene Skizzen.

Vertiefung zum Sonderfall Mixed Content: [MixedContentPatching.md](MixedContentPatching.md).

## Die drei Rollen

```
   doc1 (alt) ──┐
                ├─► XmlDiff.Compare() ─► Diffgram (XDL)
   doc2 (neu) ──┘                            │
                                             ▼
   doc1 ────────────────────────────► XmlPatch.Patch() ─► doc2
```

- **XmlDiff** vergleicht zwei Dokumente und schreibt die Unterschiede als *Diffgram* im
  XDL-Format (XML Diff Language).
- **XmlPatch** wendet ein Diffgram auf ein Dokument an.

Der Vertrag ist eng: Ein Diffgram beschreibt genau den Weg **von doc1 nach doc2**. Es ist per
Design nur auf doc1 anwendbar — nicht auf ein „ähnliches" Dokument.

## Aufbau eines Diffgrams

```xml
<xd:xmldiff version="1.0"
            srcDocHash="14079499651208002564"
            options="None"
            fragments="no"
            xmlns:xd="http://schemas.microsoft.com/xmltools/2002/xmldiff">
  ...Operationen...
</xd:xmldiff>
```

| Attribut | Bedeutung |
|---|---|
| `srcDocHash` | Hash von doc1. Der Patcher prüft damit, ob er das richtige Dokument vor sich hat |
| `options` | Die `XmlDiffOptions`, mit denen verglichen wurde (z. B. `IgnoreWhitespace`) |
| `fragments` | Ob Fragmente statt ganzer Dokumente verglichen wurden |

Der Inhalt ist ein Baum aus Operationen, der die Struktur von doc1 spiegelt.

## Das `match`-Attribut

Jede Operation adressiert ihre Knoten über `match` — **rein positionsbasiert**, ohne
Namen oder Typ:

| Ausdruck | Bedeutung |
|---|---|
| `match="2"` | zweites Kind des aktuellen Knotens |
| `match="2-4"` | Kinder 2 bis 4 |
| `match="1\|3"` | Kinder 1 und 3 |
| `match="*"` | alle Kinder |
| `match="@qty"` | das Attribut `qty` |
| `match="@*"` | alle Attribute |
| `match="/1/3"` | absoluter Pfad ab Dokumentwurzel (für Moves) |

`match="2"` heißt also schlicht „zweites Kind" — **nicht** „das `<item>`-Element". Das ist der
Kern der Fragilität des Formats, siehe unten.

> **Whitespace:** Welche Knoten mitgezählt werden, hängt davon ab, wie das Dokument geladen
> wurde (`XmlDocument.PreserveWhitespace`) und welche `XmlDiffOptions` gesetzt sind. Diff- und
> Patch-Seite müssen hier gleich konfiguriert sein, sonst zeigen alle `match`-Positionen daneben.

## Die Operationen

### `xd:node` — absteigen

Ändert selbst nichts. Sie sagt: „Im Knoten an dieser Position gibt es Änderungen an den
Kindern." Ein leeres `<xd:node match="1"/>` bedeutet „Kind 1 ist unverändert" — das ist ein
Platzhalter, damit die Positionszählung der Geschwister stimmt.

### `xd:change` — ändern

Ändert den Wert eines Attributs oder Textknotens, oder benennt ein Element um:

```xml
<xd:change match="@status">shipped</xd:change>   <!-- Attributwert -->
<xd:change match="1">DHL</xd:change>             <!-- Textinhalt -->
<xd:change match="4" name="shipping"> ... </xd:change>   <!-- Element umbenannt -->
```

Zusätzlich möglich: `ns` und `prefix` für Namespace- bzw. Präfixänderungen.

### `xd:add` — hinzufügen

Drei Varianten:

```xml
<xd:add type="2" name="id">x</xd:add>       <!-- neues Attribut (type=2) -->
<xd:add type="1" name="isbn">               <!-- neues Element (type=1) -->
  <xd:add>123</xd:add>                      <!-- dessen Textinhalt -->
</xd:add>
<xd:add match="/1/3" opid="1"/>             <!-- Move: Knoten von woanders hierher -->
```

`type` ist der numerische `XmlNodeType` (`1`=Element, `2`=Attribut, `3`=Text, `4`=CDATA,
`8`=Kommentar). Ein `xd:add` **ohne** `match` fügt neuen Inhalt ein; eines **mit** `match`
(absoluter Pfad) verschiebt bestehenden Inhalt.

### `xd:remove` — entfernen

```xml
<xd:remove match="3" opid="1"/>              <!-- ganzer Teilbaum -->
<xd:remove match="1" subtree="no"> ... </xd:remove>   <!-- nur der Knoten, Kinder bleiben -->
```

`subtree="no"` heißt: Der Knoten selbst verschwindet, seine Kinder werden weiterverarbeitet
(die enthaltenen Operationen beschreiben, was mit ihnen passiert).

### `xd:descriptor` — Move-Klammer

```xml
<xd:descriptor opid="1" type="move"/>
```

Ein Move ist ein `xd:remove` an der alten und ein `xd:add` an der neuen Stelle, verbunden über
dieselbe `opid`. Der `xd:descriptor` am Ende des Diffgrams sagt, dass es sich um eine
zusammengehörige Operation handelt (statt um Löschen + Neuanlegen).

## Vollständiges Beispiel

**doc1:**

```xml
<order id="1" status="open">
  <customer>Meier</customer>
  <item sku="A-100" qty="2">Kabel</item>
  <item sku="B-200" qty="1">Stecker</item>
  <note>bitte schnell</note>
</order>
```

**doc2** — Status geändert, die beiden `<item>` vertauscht, `qty` erhöht, `<note>` in
`<shipping>` umbenannt mit neuem Inhalt:

```xml
<order id="1" status="shipped">
  <customer>Meier</customer>
  <item sku="B-200" qty="1">Stecker</item>
  <item sku="A-100" qty="3">Kabel</item>
  <shipping>DHL</shipping>
</order>
```

**Diffgram** (`XmlDiff.Compare(doc1, doc2)`):

```xml
<xd:xmldiff version="1.0" srcDocHash="14079499651208002564" options="None" fragments="no"
            xmlns:xd="http://schemas.microsoft.com/xmltools/2002/xmldiff">
  <xd:node match="1">
    <xd:change match="@status">shipped</xd:change>
    <xd:node match="1" />
    <xd:add match="/1/3" opid="1" />
    <xd:node match="2">
      <xd:change match="@qty">3</xd:change>
    </xd:node>
    <xd:remove match="3" opid="1" />
    <xd:change match="4" name="shipping">
      <xd:change match="1">DHL</xd:change>
    </xd:change>
  </xd:node>
  <xd:descriptor opid="1" type="move" />
</xd:xmldiff>
```

Zeile für Zeile:

| Zeile | Bedeutung |
|---|---|
| `xd:node match="1"` | Absteigen in Kind 1 des Dokuments = `<order>` |
| `xd:change match="@status"` | `status` wird `shipped` |
| `xd:node match="1"` (leer) | `<customer>` unverändert — hält die Position |
| `xd:add match="/1/3" opid="1"` | Der Knoten `/1/3` (= `<item sku="B-200">`) wird **hier** eingefügt |
| `xd:node match="2"` | Absteigen in `<item sku="A-100">` … |
| `xd:change match="@qty"` | … dort `qty` auf `3` |
| `xd:remove match="3" opid="1"` | `<item sku="B-200">` an alter Stelle entfernen — gleiche `opid`, also ein Move |
| `xd:change match="4" name="shipping"` | `<note>` (Kind 4) heißt jetzt `<shipping>` … |
| `xd:change match="1">DHL` | … und sein Textkind wird `DHL` |
| `xd:descriptor opid="1" type="move"` | Deklariert `opid=1` als Verschiebung |

`XmlPatch.Patch(doc1, diffgram)` liefert exakt doc2.

## Warum das fragil ist

Alle Adressen sind Positionen. Das funktioniert perfekt, solange gepatcht wird, wofür das
Diffgram erzeugt wurde. Sobald das Zieldokument abweicht — andere Reihenfolge, übersetzte
Texte, zusätzliche Knoten — zeigen die Positionen woandershin. Bei Mixed Content passiert das
schon bei kosmetischen Umstellungen:

```xml
<p>Hallo <b>Welt</b></p>       <!-- <b> ist Kind 2 -->
<p><b>World</b> hello</p>      <!-- <b> ist Kind 1, Kind 2 ist Text -->
```

Deshalb der `srcDocHash`: Der Patcher lehnt fremde Dokumente von vornherein ab. Wer ein
Diffgram trotzdem auf ein drittes Dokument anwenden will (klassisch: Änderungen aus einer
Ausgangssprache in eine Übersetzung übertragen), verlässt den Vertrag des Formats und braucht
Zusatzinformation, um die gemeinten Knoten wiederzufinden.

## Was die Änderungen auf diesem Branch anders machen

Drei Schalter, alle **default `false`** — ohne sie verhält sich die Bibliothek exakt wie das
Original:

| Schalter | Klasse | Zweck |
|---|---|---|
| `EmitMatchValidation` | `XmlDiff` | Schreibt zu jeder Operation, **welchen Knoten** sie erwartet |
| `EnableMatchReanchoring` | `XmlPatch` | Sucht den gemeinten Knoten inhaltsbasiert statt nur über die Position |
| `IgnoreSrcValidation` | `XmlPatch` | Erlaubt abweichende Zieldokumente (überspringt `srcDocHash` und Inhaltsvergleich) |

### 1. Das Diffgram sagt jetzt, was es erwartet

Dasselbe Diffgram ohne und mit `EmitMatchValidation`:

```xml
<!-- vorher -->
<xd:node match="1">
  <xd:node match="2">
    <xd:change match="1">schoene Welt</xd:change>
  </xd:node>
  <xd:change match="3">, guten </xd:change>
  <xd:add><i>Tag</i>.</xd:add>
</xd:node>

<!-- nachher -->
<xd:node match="1" matchType="1" matchName="p" matchHash="12651728675343094072">
  <xd:node match="2" matchType="1" matchName="b" matchHash="9239324775955320286">
    <xd:change match="1" matchType="3" matchHash="15256024277234746033">schoene Welt</xd:change>
  </xd:node>
  <xd:change match="3" matchType="3" matchHash="6170666205003252982">, guten </xd:change>
  <xd:add anchorLast="yes"><i>Tag</i>.</xd:add>
</xd:node>
```

Aus `match="2"` („zweites Kind") wird `match="2"` + „das ist ein Element namens `b` mit
folgendem Inhalts-Hash". `anchorLast="yes"` markiert neuen Inhalt, der in doc2 am Ende des
Elternknotens steht.

Das sind zusätzliche Attribute in einem bestehenden Format — alte Patcher ignorieren sie.

### 2. Der Patcher kann den Knoten wiederfinden

Auflösungsreihenfolge (Details in [MixedContentPatching.md](MixedContentPatching.md)):
Position → Hash-Vergleich → Re-Anchoring per Hash → Re-Anchoring per Typ+Name → Fehler.

### 3. Falsches Patchen wird zu einem Fehler statt zu einem falschen Ergebnis

Das ist der eigentliche Gewinn. Zwei reale Durchläufe, jeweils gegen den Stand von `master`
und gegen diesen Branch.

**Fall A — Attribut hinzufügen, Zieldokument hat andere Knotenreihenfolge**

```xml
doc1:  <p>Hallo <b>Welt</b>, guten Tag.</p>
doc2:  <p>Hallo <b id="x">Welt</b>, guten Tag.</p>
Ziel:  <p><b>World</b> hello, good day.</p>
```

| Stand | Ergebnis |
|---|---|
| `master`, `IgnoreSrcValidation` | `<p><b>World</b> hello, good day.</p>` — **`id="x"` wird stillschweigend verschluckt** |
| Branch, ohne Reanchoring | `FEHLER: … '2' matched a node of type Text, but the diffgram was generated for a node of type Element …` |
| Branch, mit Reanchoring | `<p><b id="x">World</b> hello, good day.</p>` ✔ |

**Fall B — Attributänderung, Textänderung und neues Element, Zielreihenfolge vertauscht**

```xml
doc1:  <p><g id="1"/>alter text</p>
doc2:  <p><g id="2"/>neuer text<x>Neu</x></p>
Ziel:  <p>uebersetzter text<g id="1"/></p>
```

| Stand | Ergebnis |
|---|---|
| `master`, `IgnoreSrcValidation` | `FEHLER: Object reference not set to an instance of an object.` — NullReferenceException |
| Branch, mit Reanchoring | `<p>neuer text<g id="2" /><x>Neu</x></p>` ✔ |

Zusammengefasst, was besser ist:

1. **Keine stillen Fehlpatches mehr.** Wo `master` eine Änderung kommentarlos verwirft oder auf
   dem falschen Knoten anwendet, gibt es jetzt entweder das richtige Ergebnis oder eine
   Fehlermeldung, die den `match`-Pfad und die Abweichung benennt.
2. **Keine NullReferenceException.** Strukturelle Unstimmigkeiten werden vorher abgefangen.
3. **Diffgrams sind auf abweichende Dokumente anwendbar** — kontrolliert, mit klaren Regeln
   und dokumentierten Grenzen, statt per Zufall.
4. **Nichts davon ist Default.** Ohne die Schalter ist der Diffgram-Output byteidentisch zu
   vorher und das Patch-Verhalten unverändert. Alte Diffgrams bleiben lesbar, neue bleiben von
   alten Patchern lesbar.

Zwei der Prüfungen greifen auch ohne `EmitMatchValidation`, weil sie sich allein aus dem
Knotentyp am Zielort ergeben: „cannot contain children" und „does not fit the matched … node".
Genau diese beiden verwandeln in `master` stille Fehlpatches in Fehlermeldungen.

## Wo im Code

| Datei | Inhalt |
|---|---|
| [XmlDiff.cs](../src/XmlDiff/XmlDiff.cs) | Vergleich, `EmitMatchValidation` |
| [DiffgramGenerator.cs](../src/XmlDiff/DiffgramGenerator.cs) | Baut den Operationsbaum |
| [DiffgramOperation.cs](../src/XmlDiff/DiffgramOperation.cs) | Serialisierung aller `xd:*`-Operationen |
| [PathDescriptorParser.cs](../src/XmlDiff/PathDescriptorParser.cs) | Auswertung der `match`-Ausdrücke |
| [XmlPatch.cs](../src/XmlDiff/XmlPatch.cs) | Auflösung, Validierung, Re-Anchoring |
| [XmlPatchOperations.cs](../src/XmlDiff/XmlPatchOperations.cs) | Anwenden der Operationen |
| [XmlPatchError.cs](../src/XmlDiff/XmlPatchError.cs) | Fehlermeldungen |
