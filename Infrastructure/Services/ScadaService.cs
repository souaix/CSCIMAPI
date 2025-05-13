using Core.Entities.Scada;
using Core.Interfaces;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
	public class ScadaService : IScadaService
	{
		public async Task<bool> WriteTagAsync(ScadaWriteRequest request)
		{
			try
			{
				var config = new ApplicationConfiguration()
				{
					ApplicationName = "ScadaTagWriter",
					ApplicationType = ApplicationType.Client,
					SecurityConfiguration = new SecurityConfiguration
					{
						ApplicationCertificate = new CertificateIdentifier(),
						AutoAcceptUntrustedCertificates = true,
						RejectSHA1SignedCertificates = false,
						AddAppCertToTrustedStore = false,
						TrustedPeerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = "pki/trusted" },
						TrustedIssuerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = "pki/issuers" },
						RejectedCertificateStore = new CertificateTrustList { StoreType = "Directory", StorePath = "pki/rejected" }
					},
					TransportQuotas = new TransportQuotas
					{
						OperationTimeout = 15000
					},
					ClientConfiguration = new ClientConfiguration
					{
						DefaultSessionTimeout = 60000
					}
				};

				await config.Validate(ApplicationType.Client);

				var selectedEndpoint = CoreClientUtils.SelectEndpoint(request.EndpointUrl, useSecurity: false);
				var endpointConfig = EndpointConfiguration.Create(config);
				var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfig);

				using var session = await Session.Create(
					config,
					endpoint,
					false,
					"ScadaSession",
					60000,
					null,
					null
				);

				var convertedValue = ConvertJsonElementToPrimitive(request.Value);

				Console.WriteLine($"[ScadaService] 寫入值型別: {convertedValue?.GetType().Name}");

				var writeValue = new WriteValue
				{
					NodeId = new NodeId(request.NodeId),
					AttributeId = Attributes.Value,
					Value = new DataValue(new Variant(convertedValue))
				};

				var writeCollection = new WriteValueCollection { writeValue };

				var writeResponse = await session.WriteAsync(
					new RequestHeader(),
					writeCollection,
					CancellationToken.None
				);

				return StatusCode.IsGood(writeResponse.Results[0]);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[ScadaService] Tag 寫入失敗: {ex.Message}");
				return false;
			}
		}

		// ✅ JsonElement → primitive 類型 (ushort, int, bool, string)
		private object ConvertJsonElementToPrimitive(object value)
		{
			if (value is JsonElement jsonElement)
			{
				switch (jsonElement.ValueKind)
				{
					case JsonValueKind.Number:
						if (jsonElement.TryGetUInt16(out ushort us)) return us;
						if (jsonElement.TryGetInt32(out int i)) return (ushort)i;
						if (jsonElement.TryGetDouble(out double d)) return (ushort)d;
						break;

					case JsonValueKind.String:
						var str = jsonElement.GetString();
						if (ushort.TryParse(str, out ushort parsedUs)) return parsedUs;
						return str;

					case JsonValueKind.True:
					case JsonValueKind.False:
						return jsonElement.GetBoolean();
				}
			}

			return value;
		}
	}
}
