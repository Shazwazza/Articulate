#nullable enable
using Articulate.Controllers;
using NUnit.Framework;

namespace Articulate.Tests.Controllers
{
    [TestFixture]
    public class MetaWeblogControllerTests
    {
        [Test]
        public void IsValidXmlRpcEnvelope_returns_true_for_valid_method_call()
        {
            const string content = """
                                   <?xml version="1.0"?>
                                   <methodCall>
                                     <methodName>blogger.getUsersBlogs</methodName>
                                     <params><param><value>key</value></param></params>
                                   </methodCall>
                                   """;

            Assert.That(MetaWeblogController.IsValidXmlRpcEnvelope(content), Is.True);
        }

        [Test]
        public void IsValidXmlRpcEnvelope_returns_false_when_root_is_not_methodCall()
        {
            const string content = """
                                   <?xml version="1.0"?>
                                   <methodResponse>
                                     <params><param><value>ok</value></param></params>
                                   </methodResponse>
                                   """;

            Assert.That(MetaWeblogController.IsValidXmlRpcEnvelope(content), Is.False);
        }

        [Test]
        public void IsValidXmlRpcEnvelope_returns_false_when_methodName_element_is_absent()
        {
            const string content = """
                                   <?xml version="1.0"?>
                                   <methodCall>
                                     <params><param><value>key</value></param></params>
                                   </methodCall>
                                   """;

            Assert.That(MetaWeblogController.IsValidXmlRpcEnvelope(content), Is.False);
        }

        [Test]
        public void IsValidXmlRpcEnvelope_returns_false_for_malformed_xml()
        {
            const string content = "<methodCall><methodName>foo</not-closed>";

            Assert.That(MetaWeblogController.IsValidXmlRpcEnvelope(content), Is.False);
        }

        [Test]
        public void IsValidXmlRpcEnvelope_returns_false_for_empty_string() =>
            Assert.That(MetaWeblogController.IsValidXmlRpcEnvelope(string.Empty), Is.False);

        [Test]
        public void IsValidXmlRpcEnvelope_returns_false_for_plain_text() =>
            Assert.That(MetaWeblogController.IsValidXmlRpcEnvelope("not xml at all"), Is.False);
    }
}
