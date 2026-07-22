Feature: Wiring external service proxy into the builder pipeline

  Scenario: AddExternalServiceProxy registers the resource
    Given a ToxiProxyResource with no proxies registered
    And an ExternalServiceResource "weather-api" at "http://api.weather.com:80"
    When AddExternalServiceProxy("weatherProxy", 8667, weatherApi) is called
    Then ExternalServiceResources contains exactly one entry named "weatherProxy"
    And that entry has Port 8667

  Scenario: Toxic builder methods chain from AddExternalServiceProxy
    Given a ToxicExternalServiceResource builder returned from AddExternalServiceProxy
    When AddLatency("slow", latency: 200) is called on it
    Then the return value is the same builder instance
    And ToxiResources contains one toxic of type Latency

  Scenario: WithReference injects the proxy URL under the underlying service name
    Given a ToxicExternalServiceResource "weatherProxy" wrapping "weather-api" on port 8667
    And an environment-variable-capable resource
    When WithReference(toxicWeatherApi) is called
    Then the environment variable "services__weather-api__http__0"
         is set to "http://localhost:8667"
