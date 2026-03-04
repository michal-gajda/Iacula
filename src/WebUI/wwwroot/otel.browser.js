import { WebTracerProvider } from "https://cdn.jsdelivr.net/npm/@opentelemetry/sdk-trace-web@1.30.1/+esm";
import { BatchSpanProcessor } from "https://cdn.jsdelivr.net/npm/@opentelemetry/sdk-trace-base@1.30.1/+esm";
import { OTLPTraceExporter } from "https://cdn.jsdelivr.net/npm/@opentelemetry/exporter-trace-otlp-http@0.57.2/+esm";
import { Resource } from "https://cdn.jsdelivr.net/npm/@opentelemetry/resources@1.30.1/+esm";
import { registerInstrumentations } from "https://cdn.jsdelivr.net/npm/@opentelemetry/instrumentation@0.57.2/+esm";
import { FetchInstrumentation } from "https://cdn.jsdelivr.net/npm/@opentelemetry/instrumentation-fetch@0.57.2/+esm";

const provider = new WebTracerProvider({
  resource: new Resource({
    "service.name": "webui",
    "service.namespace": "iacula",
    "service.version": "poc",
    "client.type": "webassembly"
  })
});

provider.addSpanProcessor(
  new BatchSpanProcessor(
    new OTLPTraceExporter({
      url: `${window.location.origin}/otlp/v1/traces`
    })
  )
);

provider.register();

registerInstrumentations({
  instrumentations: [
    new FetchInstrumentation({
      propagateTraceHeaderCorsUrls: [/.*/],
      clearTimingResources: true
    })
  ]
});

window.__otelBrowserInitialized = true;
