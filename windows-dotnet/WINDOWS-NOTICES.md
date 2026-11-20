# Windows port dependencies

This Windows port is derived from qzz0518/Charker v0.5.1, under the upstream MIT license in LICENSE. The original macOS source and its THIRD-PARTY-NOTICES.md are provided with the source distribution. Anker trademarks, product images and 3D models remain subject to the original notices; their inclusion does not assert ownership or Anker endorsement.

Additional dependencies:

- Microsoft .NET 8 runtime / Windows Desktop runtime, MIT, Microsoft. Runtime license files are included under licenses/.
- Microsoft Windows SDK .NET projections and WebView2 SDK, Microsoft license terms included under licenses/. The setup distribution also embeds Microsoft's digitally signed Evergreen WebView2 x64 Standalone Installer, downloaded from https://go.microsoft.com/fwlink/?linkid=2124701 . This Microsoft component is installed only when the Runtime is missing, following Microsoft's offline deployment guidance: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution . It remains subject to Microsoft's WebView2 Runtime license terms.
- System.Security.Cryptography.ProtectedData 8.0.0, MIT.
- Node.js runtime, Node.js contributors, license under licenses/Node-LICENSE.txt.
- matter.js 0.16.5 and its @matter packages, Apache-2.0; package licenses included in Bridge/node_modules.
- Three.js 0.180.0, MIT; Draco decoder included from Three.js distribution. Three.js license and Draco license included under licenses/.
- qrcode 1.5.4, MIT, and transitive npm dependencies; licenses in Bridge/node_modules.

The Windows code is supplied for personal use and testing under the same MIT license as the original project. No signing certificate or Anker/Apple certification is asserted. The generated test fixtures originate in the supplied source; original provenance is documented in the upstream notices.

CUKTECH AD1204U additions adapt Apache-2.0 portions from zuyan9/ha-cuk-ble. See licenses/cuktech-NOTICE.txt and licenses/cuktech-Apache-2.0.txt for attribution and terms. These portions are excepted from the general MIT statement above. Official CUKTECH reference photos are not distributed; the new model and SVG are original illustrations.
