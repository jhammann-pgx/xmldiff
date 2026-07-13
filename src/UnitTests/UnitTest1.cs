using Microsoft.XmlDiffPatch;
using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using Xunit;

namespace UnitTests
{
    public class UnitTest1
    {
        [Fact]
        public void SimpleAddition()
        {
            XmlDocument doc1 = new XmlDocument();
            doc1.LoadXml("<a></a>");
            XmlDocument doc2 = new XmlDocument();
            doc2.LoadXml("<a><b/></a>");

            XmlDiff diff = new XmlDiff();

            var sw = new StringWriter();
            var settings = new XmlWriterSettings() { OmitXmlDeclaration = true, Indent = false };
            using (var writer = XmlWriter.Create(sw, settings))
            {
                Assert.False(diff.Compare(doc1, doc2, writer));                
            }

            XmlDocument result = new XmlDocument();
            result.LoadXml(sw.ToString());

            var inner = ToComparibleString(result);
            var expected = "<xd:xmldiff version=\"1.0\" options=\"None\" fragments=\"no\" xmlns:xd=\"http://schemas.microsoft.com/xmltools/2002/xmldiff\"><xd:node match=\"1\"><xd:add><b /></xd:add></xd:node></xd:xmldiff>";
            Assert.Equal(inner, expected);
        }

        [Fact]
        public void SimpleFragment()
        {
            using (var col = new TempFileCollection())
            {
                var file1 = col.CreateTempFile("<a></a><b></b>");
                var file2 = col.CreateTempFile("<a></a><b></b><c></c>");

                XmlDiff diff = new XmlDiff();

                var sw = new StringWriter();
                var settings = new XmlWriterSettings() { OmitXmlDeclaration = true, Indent = false };
                using (var writer = XmlWriter.Create(sw, settings))
                {
                    Assert.False(diff.Compare(file1, file2, true, writer));
                }

                XmlDocument result = new XmlDocument();
                result.LoadXml(sw.ToString());

                var inner = ToComparibleString(result);
                var expected = "<xd:xmldiff version=\"1.0\" options=\"None\" fragments=\"yes\" xmlns:xd=\"http://schemas.microsoft.com/xmltools/2002/xmldiff\"><xd:node match=\"2\" /><xd:add><c /></xd:add></xd:xmldiff>";
                Assert.Equal(inner, expected);
            }
        }

        [Fact]
        public void SideBySideDiffView()
        {
            using (var col = new TempFileCollection())
            {
                var xmlSource = col.CreateTempFile("<root><a></a><b><temp/></b></root>");
                var xmlChanged = col.CreateTempFile("<root><a id='1'></a><b></b><c></c></root>");

                var view = new XmlDiffView();
                var options = XmlDiffOptions.IgnoreChildOrder | XmlDiffOptions.IgnoreWhitespace;
                var results = view.DifferencesSideBySideAsHtml(xmlSource, xmlChanged, false, options);

                var diff = results.ReadToEnd();
                Assert.Contains("<span class=\"remove\">&lt;temp</span>", diff);
                Assert.Contains("<span class=\"add\">id=\"1\"</span>", diff);
                Assert.Contains("<span class=\"add\">&lt;c</span>", diff);
            }
        }

        [Fact]
        public void RegressionMix()
        {
            using (var col = new TempFileCollection())
            {
                var xmlSource = col.CreateTempFile("<Profile><field><field>Approval_Workflow__c.Description__c</field></field><field><editable>false</editable><field>Approval_Workflow__c.Migration_Id__c</field><readable>false</readable></field><field><field>Approval_Workflow__c.Reject_PO_Substage__c</field></field><field><field>Location.VisitorAddressId</field></field><flow><flow>TestAccListApexFlow</flow></flow><layout><layout>Lead-LeadLayout</layout></layout><layout><layout>Macro-MacroLayout</layout></layout><layout><layout>OFAC__SDN_Match__c-OFAC__MatchLayout</layout></layout><layout><layout>OFAC__SDN_Search__c-OFAC__OFACRequestLayout</layout></layout><layout><layout>Oak_Office__c-OakOfficeLayout</layout></layout><objectPermissions><allowCreate>false</allowCreate><allowDelete>false</allowDelete><allowEdit>false</allowEdit><allowRead>true</allowRead><modifyAllRecords>false</modifyAllRecords></objectPermissions><objectPermissions><object>Asset</object><viewAllRecords>false</viewAllRecords></objectPermissions></Profile>");
                var xmlChanged = col.CreateTempFile("<Profile><field><field>Approval_Workflow__c.Description__c</field></field><field><editable>true</editable><field>Approval_Workflow__c.Migration_Id__c</field><readable>true</readable></field><field><field>Approval_Workflow__c.Reject_PO_Substage__c</field></field><field><field>Location.VisitorAddressId</field></field><layout><layout>Lead-LeadLayout</layout></layout><layout><layout>Location-LocationLayout</layout></layout><layout><layout>Macro-MacroLayout</layout></layout><layout><layout>Oak_Office__c-OakOfficeLayout</layout></layout><objectPermissions><allowCreate>false</allowCreate><allowDelete>false</allowDelete><allowEdit>false</allowEdit><allowRead>true</allowRead><modifyAllRecords>false</modifyAllRecords><object>Address</object><viewAllRecords>false</viewAllRecords></objectPermissions><objectPermissions><allowCreate>false</allowCreate><allowDelete>false</allowDelete><allowEdit>false</allowEdit><allowRead>true</allowRead><modifyAllRecords>false</modifyAllRecords><object>Asset</object><viewAllRecords>false</viewAllRecords></objectPermissions></Profile>");

                var view = new XmlDiffView();
                var options = XmlDiffOptions.IgnoreChildOrder | XmlDiffOptions.IgnoreWhitespace;
                var results = view.DifferencesSideBySideAsHtml(xmlSource, xmlChanged, false, options);

                var diff = results.ReadToEnd();
                Assert.Contains("<span class=\"remove\">&lt;temp</span>", diff);
                Assert.Contains("<span class=\"add\">id=\"1\"</span>", diff);
                Assert.Contains("<span class=\"add\">&lt;c</span>", diff);
            }
        }

        [Fact]
        public void SideBySideDiffViewCompact()
        {
            using (var col = new TempFileCollection())
            {
                var xmlSource = col.CreateTempFile("<root id='3'><apple></apple><banana><temp/></banana><elephant size='big'/></root>");
                var xmlChanged = col.CreateTempFile("<root id='3'><apple></apple><banana></banana><carrot/><elephant size='big'/></root>");

                var view = new XmlDiffView();
                var options = XmlDiffOptions.IgnoreChildOrder | XmlDiffOptions.IgnoreWhitespace;
                var results = view.DifferencesSideBySideAsHtml(xmlSource, xmlChanged, false, options, true);

                var diff = results.ReadToEnd();
                Assert.Contains("root", diff);
                Assert.Contains("banana", diff);
                Assert.Contains("&lt;temp", diff);
                Assert.DoesNotContain("apple", diff);
                Assert.DoesNotContain("elephant", diff);
            }
        }

        [Fact]
        public void PatchCanIgnoreSourceValidation()
        {
            XmlDocument originalSource = new XmlDocument();
            originalSource.LoadXml("<a></a>");

            XmlDocument changedSource = new XmlDocument();
            changedSource.LoadXml("<a><b/></a>");

            XmlDiff diff = new XmlDiff();
            string diffgram;
            var settings = new XmlWriterSettings() { OmitXmlDeclaration = true, Indent = false };
            using (var sw = new StringWriter())
            {
                using (var writer = XmlWriter.Create(sw, settings))
                {
                    Assert.False(diff.Compare(originalSource, changedSource, writer));
                }

                diffgram = sw.ToString();
            }

            XmlDocument mismatchedSource = new XmlDocument();
            mismatchedSource.LoadXml("<a c='1'></a>");

            XmlDocument diffgramDocument = new XmlDocument();
            diffgramDocument.LoadXml(diffgram);

            XmlPatch patch = new XmlPatch();
            Assert.Throws<Exception>(() =>
            {
                using (var reader = new XmlNodeReader(diffgramDocument))
                {
                    patch.Patch(mismatchedSource, reader);
                }
            });

            patch.IgnoreSrcValidation = true;
            using (var reader = new XmlNodeReader(diffgramDocument))
            {
                patch.Patch(mismatchedSource, reader);
            }

            Assert.Equal("<a c=\"1\"><b /></a>", mismatchedSource.DocumentElement.OuterXml);
        }

        [Fact]
        public void PatchMixedContentRoundtripStillWorks()
        {
            // reordering text and inline element patches cleanly when applied to the original source
            var patched = DiffAndPatch(
                "<p><b>bold</b>Text</p>",
                "<p>Text<b>bold</b></p>",
                "<p><b>bold</b>Text</p>",
                XmlDiffOptions.None);

            Assert.Equal("<p>Text<b>bold</b></p>", patched);
        }

        [Fact]
        public void PatchDescendingIntoLeafNodeFailsWithDescriptiveError()
        {
            // Diffgram generated for <p>Text<b>bold</b></p>, where <b> is child 2.
            // In the patched document the inline element comes first, so the positional
            // match="2" resolves to the text node. With IgnoreChildOrder the srcDocHash
            // is order-independent and does not catch this.
            var ex = Assert.Throws<Exception>(() => DiffAndPatch(
                "<p>Text<b>bold</b></p>",
                "<p>Text<b>fett</b></p>",
                "<p><b>bold</b>Text</p>",
                XmlDiffOptions.IgnoreChildOrder));

            Assert.Contains("cannot contain children", ex.Message);
        }

        [Fact]
        public void PatchTextChangeHittingElementFailsInsteadOfSilentNoOp()
        {
            // The text-value change targets child 1, which is the <b> element in the
            // patched document. Previously this was silently ignored.
            var ex = Assert.Throws<Exception>(() => DiffAndPatch(
                "<p>Text<b>bold</b></p>",
                "<p>Other<b>bold</b></p>",
                "<p><b>bold</b>Text</p>",
                XmlDiffOptions.IgnoreChildOrder));

            Assert.Contains("does not fit the matched Element node", ex.Message);
        }

        [Fact]
        public void PatchRenameChangeHittingTextNodeFailsInsteadOfCorruptingValue()
        {
            // Hand-crafted diffgram: an element rename targeting child 1, which is a text
            // node in the patched document. Previously the text value was overwritten with
            // the change operation's (empty) content.
            string diffgram = "<xd:xmldiff version=\"1.0\" options=\"None\" fragments=\"no\"" +
                " xmlns:xd=\"http://schemas.microsoft.com/xmltools/2002/xmldiff\">" +
                "<xd:node match=\"1\"><xd:change match=\"1\" name=\"i\" /></xd:node></xd:xmldiff>";

            XmlDocument patchDoc = new XmlDocument();
            patchDoc.LoadXml("<p>Text<b>bold</b></p>");

            XmlPatch patch = new XmlPatch() { IgnoreSrcValidation = true };
            var ex = Assert.Throws<Exception>(() =>
            {
                using (var reader = XmlReader.Create(new StringReader(diffgram)))
                {
                    patch.Patch(patchDoc, reader);
                }
            });

            Assert.Contains("does not fit the matched Text node", ex.Message);
        }

        [Fact]
        public void MatchValidationRoundtripPatchesCleanly()
        {
            // the emitted matchType/matchName/matchHash attributes must not disturb a
            // legitimate patch, and the diff-side node hashes must equal the hashes
            // recomputed from the DOM on the patch side
            string diffgram = Diff(
                "<p><b>bold</b>Text</p>",
                "<p>Text<b>fett</b></p>",
                XmlDiffOptions.None,
                emitMatchValidation: true);

            Assert.Contains("matchHash", diffgram);

            var patched = ApplyPatch(diffgram, "<p><b>bold</b>Text</p>");
            Assert.Equal("<p>Text<b>fett</b></p>", patched);
        }

        [Fact]
        public void MatchValidationDetectsRemovalOfWrongNode()
        {
            // The removal targets child 3, which is <i> in the diff source but <b> in the
            // patched document. Without match metadata the wrong element is silently removed;
            // srcDocHash does not catch it because it is order-independent under IgnoreChildOrder.
            string diffgram = Diff(
                "<p>Text<b>bold</b><i>x</i></p>",
                "<p>Text<b>bold</b></p>",
                XmlDiffOptions.IgnoreChildOrder,
                emitMatchValidation: true);

            var ex = Assert.Throws<Exception>(() => ApplyPatch(diffgram, "<p><i>x</i>Text<b>bold</b></p>"));

            Assert.Contains("named", ex.Message);
        }

        [Fact]
        public void MatchValidationDetectsContentDrift()
        {
            // Both documents contain the same two <f> elements, only in swapped order, so the
            // order-independent srcDocHash under IgnoreChildOrder passes. The positional match
            // then hits the wrong same-named element; only the node content hash catches this.
            string diffgram = Diff(
                "<r><f>A</f><f>B</f></r>",
                "<r><f>C</f><f>B</f></r>",
                XmlDiffOptions.IgnoreChildOrder,
                emitMatchValidation: true);

            var ex = Assert.Throws<Exception>(() => ApplyPatch(diffgram, "<r><f>B</f><f>A</f></r>"));

            Assert.Contains("differs from the corresponding", ex.Message);
        }

        [Fact]
        public void MatchValidationWithIgnoreSrcValidationSkipsHashButKeepsTypeAndName()
        {
            // IgnoreSrcValidation declares that the documents may differ in content, so the
            // node hash is not enforced - but node type and name still are
            string diffgram = Diff("<a></a>", "<a><b/></a>", XmlDiffOptions.None, emitMatchValidation: true);

            var patched = ApplyPatch(diffgram, "<a c='1'></a>", ignoreSrcValidation: true);
            Assert.Equal("<a c=\"1\"><b /></a>", patched);

            var ex = Assert.Throws<Exception>(() => ApplyPatch(diffgram, "<x></x>", ignoreSrcValidation: true));
            Assert.Contains("named", ex.Message);
        }

        [Fact]
        public void ReanchoringPatchesReorderedMixedContent()
        {
            // The diffgram changes the text inside <b>, addressed as child 2. In the patched
            // document <b> is child 1; re-anchoring locates it by its content hash and the
            // nested change applies inside the re-anchored element.
            string diffgram = Diff(
                "<p>Text<b>bold</b></p>",
                "<p>Text<b>fett</b></p>",
                XmlDiffOptions.None,
                emitMatchValidation: true);

            var patched = ApplyPatch(diffgram, "<p><b>bold</b>Text</p>",
                ignoreSrcValidation: true, enableReanchoring: true);

            Assert.Equal("<p><b>fett</b>Text</p>", patched);
        }

        [Fact]
        public void ReanchoringRemovesCorrectNode()
        {
            // The removal targets child 3, which is <b> in the patched document; re-anchoring
            // redirects it to the <i> element the diffgram was generated for. The srcDocHash
            // passes because it is order-independent under IgnoreChildOrder.
            string diffgram = Diff(
                "<p>Text<b>bold</b><i>x</i></p>",
                "<p>Text<b>bold</b></p>",
                XmlDiffOptions.IgnoreChildOrder,
                emitMatchValidation: true);

            var patched = ApplyPatch(diffgram, "<p><i>x</i>Text<b>bold</b></p>",
                enableReanchoring: true);

            Assert.Equal("<p>Text<b>bold</b></p>", patched);
        }

        [Fact]
        public void ReanchoringFixesSameNameSiblingSwap()
        {
            // Both <f> elements have the same name and type; only the content hash can tell
            // them apart. The change A -> C must follow the A-node to its new position.
            string diffgram = Diff(
                "<r><f>A</f><f>B</f></r>",
                "<r><f>C</f><f>B</f></r>",
                XmlDiffOptions.IgnoreChildOrder,
                emitMatchValidation: true);

            var patched = ApplyPatch(diffgram, "<r><f>B</f><f>A</f></r>",
                enableReanchoring: true);

            Assert.Equal("<r><f>B</f><f>C</f></r>", patched);
        }

        [Fact]
        public void ReanchoringToleratesAddedTextAroundChangedInlineElement()
        {
            // The patched document has additional text before and after the inline element AND
            // the element's content differs (typical for translated documents). The hash cannot
            // locate the changed <b>, so under IgnoreSrcValidation the re-anchoring falls back
            // to the nearest sibling with the expected type and name.
            string diffgram = Diff(
                "<p><b>alt</b></p>",
                "<p><b>neu</b></p>",
                XmlDiffOptions.None,
                emitMatchValidation: true);

            var patched = ApplyPatch(diffgram, "<p>Vor<b>ALT</b>Nach</p>",
                ignoreSrcValidation: true, enableReanchoring: true);

            Assert.Equal("<p>Vor<b>neu</b>Nach</p>", patched);
        }

        [Fact]
        public void ReanchoringToleratesChangedContentOnReorderedInlineElement()
        {
            // The inline element moved to the front and both text nodes differ from the diff
            // source; the nested change still finds the <b> element and applies inside it.
            string diffgram = Diff(
                "<p>Hallo<b>alt</b></p>",
                "<p>Hallo<b>neu</b></p>",
                XmlDiffOptions.None,
                emitMatchValidation: true);

            var patched = ApplyPatch(diffgram, "<p><b>ALT</b>Uebersetzt</p>",
                ignoreSrcValidation: true, enableReanchoring: true);

            Assert.Equal("<p><b>neu</b>Uebersetzt</p>", patched);
        }

        [Fact]
        public void ReanchoringToleratesAddedInlineElements()
        {
            // The patched document contains an additional inline element (<i>) that shifts all
            // positions, plus extra text and changed content in the target <b>. The type+name
            // fallback skips the foreign <i> and anchors on the <b>.
            string diffgram = Diff(
                "<p>Hallo<b>alt</b></p>",
                "<p>Hallo<b>neu</b></p>",
                XmlDiffOptions.None,
                emitMatchValidation: true);

            var patched = ApplyPatch(diffgram, "<p>Start<i>zusatz</i><b>ALT</b>Ende</p>",
                ignoreSrcValidation: true, enableReanchoring: true);

            Assert.Equal("<p>Start<i>zusatz</i><b>neu</b>Ende</p>", patched);
        }

        [Fact]
        public void ReanchoringAppliesAttributeChangeOnRepositionedInlineElement()
        {
            // The core bloXedia pre-translation requirement: an attribute changed on an inline
            // element that sits at position 1 in one document and position 2 in the other.
            // Only the attribute is patched; the mixed content itself is not repositioned.
            string diffgram = Diff(
                "<p><g a='alt' />Text</p>",
                "<p><g a='neu' />Text</p>",
                XmlDiffOptions.None,
                emitMatchValidation: true);

            var patched = ApplyPatch(diffgram, "<p>Anderer Text<g a='alt' /></p>",
                ignoreSrcValidation: true, enableReanchoring: true);

            Assert.Equal("<p>Anderer Text<g a=\"neu\" /></p>", patched);
        }

        [Fact]
        public void ReanchoringFallsBackToValidationWhenNoCandidateExists()
        {
            // The expected <b> element does not exist anywhere in the patched document, so
            // re-anchoring finds no candidate and the Stage 2 validation reports the mismatch.
            string diffgram = Diff(
                "<p>Text<b>bold</b></p>",
                "<p>Text<b>fett</b></p>",
                XmlDiffOptions.None,
                emitMatchValidation: true);

            var ex = Assert.Throws<Exception>(() => ApplyPatch(diffgram, "<p>Other<i>x</i></p>",
                ignoreSrcValidation: true, enableReanchoring: true));

            Assert.Contains("named", ex.Message);
        }

        private string Diff(string diffSource, string diffTarget, XmlDiffOptions options, bool emitMatchValidation = false)
        {
            XmlDocument sourceDoc = new XmlDocument();
            sourceDoc.LoadXml(diffSource);
            XmlDocument targetDoc = new XmlDocument();
            targetDoc.LoadXml(diffTarget);

            XmlDiff diff = new XmlDiff(options) { EmitMatchValidation = emitMatchValidation };
            var settings = new XmlWriterSettings() { OmitXmlDeclaration = true, Indent = false };
            using (var sw = new StringWriter())
            {
                using (var writer = XmlWriter.Create(sw, settings))
                {
                    diff.Compare(sourceDoc, targetDoc, writer);
                }
                return sw.ToString();
            }
        }

        private string ApplyPatch(string diffgram, string patchSource, bool ignoreSrcValidation = false, bool enableReanchoring = false)
        {
            XmlDocument patchDoc = new XmlDocument();
            patchDoc.LoadXml(patchSource);
            XmlPatch patch = new XmlPatch()
            {
                IgnoreSrcValidation = ignoreSrcValidation,
                EnableMatchReanchoring = enableReanchoring
            };
            using (var reader = XmlReader.Create(new StringReader(diffgram)))
            {
                patch.Patch(patchDoc, reader);
            }
            return patchDoc.DocumentElement.OuterXml;
        }

        private string DiffAndPatch(string diffSource, string diffTarget, string patchSource, XmlDiffOptions options)
        {
            return ApplyPatch(Diff(diffSource, diffTarget, options), patchSource);
        }

        private string ToComparibleString(XmlDocument doc)
        {
            // avoid comparing the hash.
            var s = doc.OuterXml;
            int pos = s.IndexOf("srcDocHash");
            int end = s.IndexOf("\"", pos + 12) + 2;
            s = s.Substring(0, pos) + s.Substring(end);
            return s;
        }
    }
}
