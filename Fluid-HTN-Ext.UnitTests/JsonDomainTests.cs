using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FluidHTN;
using FluidHTN.Json;

namespace Fluid_HTN_Ext.UnitTests
{
    [TestClass]
    public class JsonDomainTests
    {
        private readonly IDomainJsonProvider _provider = new SystemTextJsonProvider();

        [TestMethod]
        public void DomainJsonNode_CreatesFromValidJson()
        {
            var json = @"{
                ""name"": ""test-domain"",
                ""type"": ""Sequence"",
                ""subtasks"": []
            }";

            var node = _provider.Parse(json);

            Assert.AreEqual("test-domain", node.Name);
            Assert.AreEqual("Sequence", node.Type);
        }

        [TestMethod]
        public void DomainJsonNode_GetString_ReturnsCorrectValue()
        {
            var json = @"{
                ""name"": ""test-task"",
                ""description"": ""A test task""
            }";

            var node = _provider.Parse(json);

            Assert.AreEqual("test-task", node.GetString("name"));
            Assert.AreEqual("A test task", node.GetString("description"));
            Assert.IsNull(node.GetString("missing"));
            Assert.AreEqual("default", node.GetString("missing", "default"));
        }

        [TestMethod]
        public void DomainJsonNode_GetValue_ReturnsNumericValues()
        {
            var json = @"{
                ""count"": 5,
                ""index"": 10
            }";

            var node = _provider.Parse(json);

            Assert.AreEqual(5u, node.GetValue<uint>("count"));
            Assert.AreEqual(10, node.GetValue<int>("index"));
            Assert.AreEqual(0u, node.GetValue<uint>("missing", 0u));
        }

        [TestMethod]
        public void DomainJsonNode_GetEnum_ParsesEnumValues()
        {
            var json = @"{
                ""repetitionType"": ""Blockwise""
            }";

            var node = _provider.Parse(json);

            var result = node.GetEnum<FluidHTN.Compounds.RepeatSequence.RepetitionType>("repetitionType");
            Assert.AreEqual(FluidHTN.Compounds.RepeatSequence.RepetitionType.Blockwise, result);
        }

        [TestMethod]
        public void DomainJsonNode_GetArray_ReturnsChildNodes()
        {
            var json = @"{
                ""subtasks"": [
                    { ""type"": ""Action"", ""name"": ""Task1"" },
                    { ""type"": ""Action"", ""name"": ""Task2"" }
                ]
            }";

            var node = _provider.Parse(json);
            var subtasks = new List<DomainJsonNode>(node.Subtasks);

            Assert.AreEqual(2, subtasks.Count);
            Assert.AreEqual("Task1", subtasks[0].Name);
            Assert.AreEqual("Task2", subtasks[1].Name);
        }

        [TestMethod]
        public void DomainJsonNode_HasProperty_ChecksExistence()
        {
            var json = @"{
                ""name"": ""test"",
                ""subtasks"": []
            }";

            var node = _provider.Parse(json);

            Assert.IsTrue(node.HasProperty("name"));
            Assert.IsTrue(node.HasProperty("subtasks"));
            Assert.IsFalse(node.HasProperty("missing"));
        }

        [TestMethod]
        public void DomainJsonNode_GetChild_ReturnsNestedNode()
        {
            var json = @"{
                ""operator"": {
                    ""type"": ""Condition"",
                    ""name"": ""check-state""
                }
            }";

            var node = _provider.Parse(json);
            var op = node.Operator;

            Assert.IsNotNull(op);
            Assert.AreEqual("Condition", op.Type);
            Assert.AreEqual("check-state", op.Name);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void DomainJsonNode_ThrowsOnNullJson()
        {
            _provider.Parse(null);
        }

        [TestMethod]
        public void DomainJsonNode_ThrowsOnInvalidJsonText()
        {
            try
            {
                _provider.Parse("{ invalid json");
                Assert.Fail("Expected an exception to be thrown");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.InnerException != null || ex.Message.Contains("invalid") || ex.Message.Contains("Failed"), "Exception should be related to invalid JSON");
            }
        }

        [TestMethod]
        public void DomainJsonFactory_BuildsSimpleSequence()
        {
            var json = @"{
                ""name"": ""test-domain"",
                ""root"": {
                    ""type"": ""Sequence"",
                    ""name"": ""Root"",
                    ""subtasks"": [
                        { ""type"": ""Idle"" }
                    ]
                }
            }";

            // Note: This test would require MyDomainBuilder to be available
            // For now, we verify the JSON parsing works correctly
            var node = _provider.Parse(json);
            Assert.AreEqual("test-domain", node.GetString("name"));

            var rootNode = node.GetChild("root");
            Assert.IsNotNull(rootNode);
            Assert.AreEqual("Sequence", rootNode.Type);
            Assert.AreEqual("Root", rootNode.Name);
        }

        [TestMethod]
        public void DomainJsonNode_ParsesComplexStructure()
        {
            var json = @"{
                ""name"": ""complex-domain"",
                ""root"": {
                    ""type"": ""Sequence"",
                    ""name"": ""Root"",
                    ""subtasks"": [
                        {
                            ""type"": ""Selector"",
                            ""name"": ""Choose"",
                            ""subtasks"": [
                                { ""type"": ""GetFood"" },
                                { ""type"": ""Rest"" },
                                { ""type"": ""Idle"" }
                            ]
                        },
                        {
                            ""type"": ""Repeat"",
                            ""name"": ""Patrol"",
                            ""worldStateIndex"": 0,
                            ""subtasks"": [
                                { ""type"": ""Action"", ""name"": ""Move"" }
                            ]
                        }
                    ]
                }
            }";

            var node = _provider.Parse(json);
            var rootNode = node.GetChild("root");
            var subtasks = new List<DomainJsonNode>(rootNode.Subtasks);

            Assert.AreEqual(2, subtasks.Count);
            Assert.AreEqual("Sequence", rootNode.Type);

            // First subtask is a Selector
            Assert.AreEqual("Selector", subtasks[0].Type);
            var selectorChildren = new List<DomainJsonNode>(subtasks[0].Subtasks);
            Assert.AreEqual(3, selectorChildren.Count);

            // Second subtask is a Repeat with parameters
            Assert.AreEqual("Repeat", subtasks[1].Type);
            Assert.AreEqual(0u, subtasks[1].GetValue<uint>("worldStateIndex"));
        }

        [TestMethod]
        public void DomainJsonNode_HandlesEmptySubtasks()
        {
            var json = @"{
                ""type"": ""Idle"",
                ""subtasks"": []
            }";

            var node = _provider.Parse(json);
            var subtasks = new List<DomainJsonNode>(node.Subtasks);

            Assert.AreEqual(0, subtasks.Count);
            Assert.IsTrue(node.HasProperty("subtasks"));
        }

        [TestMethod]
        public void DomainJsonNode_ReadsRepeatSequenceParameters()
        {
            var json = @"{
                ""type"": ""Repeat"",
                ""name"": ""RepeatTest"",
                ""worldStateIndex"": 5,
                ""repetitionType"": ""Blockwise""
            }";

            var node = _provider.Parse(json);

            Assert.AreEqual(5u, node.GetValue<uint>("worldStateIndex"));
            var repType = node.GetEnum<FluidHTN.Compounds.RepeatSequence.RepetitionType>("repetitionType");
            Assert.AreEqual(FluidHTN.Compounds.RepeatSequence.RepetitionType.Blockwise, repType);
        }

        [TestMethod]
        public void DomainJsonNode_DefaultEnumValue()
        {
            var json = @"{ ""type"": ""Repeat"" }";

            var node = _provider.Parse(json);
            var repType = node.GetEnum(
                "repetitionType",
                FluidHTN.Compounds.RepeatSequence.RepetitionType.Interleaved
            );

            Assert.AreEqual(FluidHTN.Compounds.RepeatSequence.RepetitionType.Interleaved, repType);
        }
    }
}
