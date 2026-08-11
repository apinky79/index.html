import Foundation

@MainActor
final class SessionViewModel: ObservableObject {
    @Published private(set) var connectionState: ConnectionState = .idle
    @Published var username: String
    @Published var password: String
    @Published var rememberPassword = true
    @Published var fluidMode = true
    @Published private(set) var latencyMs = 0
    @Published private(set) var bandwidthKbps = 0
    @Published private(set) var framesPerSecond = 0
    @Published private(set) var eventLog: [String] = []
    @Published var errorBanner: String?

    let host: RemoteHost

    private let passwordStore: PasswordStore
    private let engine: RemoteConnectionEngine
    private var eventTask: Task<Void, Never>?

    init(
        host: RemoteHost,
        passwordStore: PasswordStore,
        engine: RemoteConnectionEngine = MockConnectionEngine()
    ) {
        self.host = host
        self.passwordStore = passwordStore
        self.engine = engine
        self.username = host.username
        self.password = passwordStore.password(for: host.id) ?? ""
        self.rememberPassword = !self.password.isEmpty
    }

    var canConnect: Bool {
        !username.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    func connect() {
        guard canConnect else {
            errorBanner = "Username is required."
            return
        }

        if rememberPassword {
            do {
                try passwordStore.setPassword(password, for: host.id)
            } catch {
                errorBanner = error.localizedDescription
            }
        } else {
            do {
                try passwordStore.setPassword(nil, for: host.id)
            } catch {
                errorBanner = error.localizedDescription
            }
        }

        eventTask?.cancel()
        connectionState = .resolving
        appendLog("Connecting to \(host.endpoint) (\(host.protocolType.rawValue))")

        let credentials = SessionCredentials(
            username: username.trimmingCharacters(in: .whitespacesAndNewlines),
            password: password
        )

        let stream = engine.connect(to: host, credentials: credentials)
        eventTask = Task { [weak self] in
            guard let self else { return }

            for await event in stream {
                await self.handle(event)
            }
        }
    }

    func disconnect() {
        engine.disconnect()
        eventTask?.cancel()
        eventTask = nil
        connectionState = .disconnected
        appendLog("Disconnected from \(host.endpoint)")
    }

    private func handle(_ event: ConnectionEvent) {
        switch event {
        case let .state(state):
            connectionState = state
            appendLog(state.title)

            if case let .failed(message) = state {
                errorBanner = message
            }
        case let .telemetry(latencyMs, bandwidthKbps, framesPerSecond):
            self.latencyMs = latencyMs
            self.bandwidthKbps = bandwidthKbps
            self.framesPerSecond = framesPerSecond
        }
    }

    private func appendLog(_ message: String) {
        let timestamp = Date.now.formatted(date: .omitted, time: .standard)
        eventLog.append("[\(timestamp)] \(message)")
        if eventLog.count > 120 {
            eventLog.removeFirst(eventLog.count - 120)
        }
    }
}
