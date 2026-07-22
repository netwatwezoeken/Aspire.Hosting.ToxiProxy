Feature: External service proxy resource construction

  Scenario: Resource captures the external service URI at construction time
    Given an ExternalServiceResource with URI "http://api.weather.com:80"
    And a ToxiProxyResource as the parent
    When a ToxicExternalServiceResource wrapping it is constructed with port 8667
    Then TargetUri has host "api.weather.com" and port 80
    And Port is 8667

  Scenario: Construction fails when external service has no static URI
    Given an ExternalServiceResource with no static URI configured
    And a ToxiProxyResource as the parent
    When a ToxicExternalServiceResource wrapping it is constructed
    Then an InvalidOperationException is thrown with the resource name in the message

  Scenario: External hostname is passed through unchanged in the computed upstream
    Given a ToxicExternalServiceResource with TargetUri "http://api.weather.com:80"
    When the upstream string is computed from the resource
    Then the upstream string is "api.weather.com:80"

  Scenario: localhost in the target URI is translated to host.docker.internal
    Given a ToxicExternalServiceResource with TargetUri "http://localhost:9000"
    When the upstream string is computed from the resource
    Then the upstream string is "host.docker.internal:9000"

  Scenario: 127.0.0.1 in the target URI is translated to host.docker.internal
    Given a ToxicExternalServiceResource with TargetUri "http://127.0.0.1:9000"
    When the upstream string is computed from the resource
    Then the upstream string is "host.docker.internal:9000"
