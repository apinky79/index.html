import SwiftUI

struct ConnectionView: View {
    @Environment(\.dismiss) private var dismiss
    @StateObject private var viewModel: SessionViewModel
    private let onConnected: () -> Void

    @State private var didSignalConnected = false

    init(host: RemoteHost, passwordStore: PasswordStore, onConnected: @escaping () -> Void) {
        _viewModel = StateObject(wrappedValue: SessionViewModel(host: host, passwordStore: passwordStore))
        self.onConnected = onConnected
    }

    var body: some View {
        NavigationStack {
            Group {
                if viewModel.connectionState.isConnected {
                    activeSession
                } else {
                    connectionForm
                }
            }
            .navigationTitle(viewModel.host.name)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Close") {
                        viewModel.disconnect()
                        dismiss()
                    }
                }
                ToolbarItem(placement: .topBarTrailing) {
                    ConnectionStateBadge(state: viewModel.connectionState)
                }
            }
        }
        .onChange(of: viewModel.connectionState) { _, newState in
            if newState.isConnected, !didSignalConnected {
                didSignalConnected = true
                onConnected()
            }
        }
        .alert(
            "Connection Error",
            isPresented: Binding(
                get: { viewModel.errorBanner != nil },
                set: { _ in viewModel.errorBanner = nil }
            )
        ) {
            Button("OK", role: .cancel) {}
        } message: {
            Text(viewModel.errorBanner ?? "")
        }
    }

    private var connectionForm: some View {
        Form {
            Section("Target") {
                LabeledContent("Endpoint", value: viewModel.host.endpoint)
                LabeledContent("Protocol", value: viewModel.host.protocolType.transportName)
            }

            Section("Credentials") {
                TextField("Username", text: $viewModel.username)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled(true)
                SecureField("Password", text: $viewModel.password)
                Toggle("Remember password", isOn: $viewModel.rememberPassword)
            }

            if !viewModel.host.notes.isEmpty {
                Section("Notes") {
                    Text(viewModel.host.notes)
                        .foregroundStyle(.secondary)
                }
            }

            Section {
                Button {
                    viewModel.connect()
                } label: {
                    Label("Connect", systemImage: "play.circle.fill")
                        .frame(maxWidth: .infinity, alignment: .center)
                }
                .buttonStyle(.borderedProminent)
                .disabled(!viewModel.canConnect)

                Button(role: .cancel) {
                    dismiss()
                } label: {
                    Text("Cancel")
                        .frame(maxWidth: .infinity, alignment: .center)
                }
            }
        }
    }

    private var activeSession: some View {
        VStack(spacing: 12) {
            RemoteDesktopSurfaceView(fluidMode: viewModel.fluidMode)

            SessionMetricsView(
                latencyMs: viewModel.latencyMs,
                bandwidthKbps: viewModel.bandwidthKbps,
                framesPerSecond: viewModel.framesPerSecond
            )

            HStack {
                Toggle("Fluid Mode", isOn: $viewModel.fluidMode)
                    .toggleStyle(.switch)

                Spacer()

                Button(role: .destructive) {
                    viewModel.disconnect()
                } label: {
                    Label("Disconnect", systemImage: "stop.fill")
                }
                .buttonStyle(.borderedProminent)
            }
            .padding(.horizontal)

            eventLog
        }
        .padding(.top, 8)
    }

    private var eventLog: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 8) {
                ForEach(Array(viewModel.eventLog.enumerated()), id: \.offset) { _, line in
                    Text(line)
                        .font(.caption.monospaced())
                        .foregroundStyle(.secondary)
                        .frame(maxWidth: .infinity, alignment: .leading)
                }
            }
            .padding(.horizontal)
            .padding(.vertical, 8)
        }
        .frame(maxHeight: 180)
        .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 12, style: .continuous))
        .padding(.horizontal)
        .padding(.bottom, 8)
    }
}

private struct ConnectionStateBadge: View {
    let state: ConnectionState

    var body: some View {
        Text(state.title)
            .font(.caption.bold())
            .padding(.horizontal, 10)
            .padding(.vertical, 6)
            .background(color.opacity(0.15), in: Capsule())
            .foregroundStyle(color)
    }

    private var color: Color {
        switch state {
        case .connected:
            return .green
        case .failed:
            return .red
        case .resolving, .connecting, .authenticating:
            return .orange
        case .disconnected, .idle:
            return .secondary
        }
    }
}

private struct SessionMetricsView: View {
    let latencyMs: Int
    let bandwidthKbps: Int
    let framesPerSecond: Int

    var body: some View {
        HStack {
            metric("Latency", "\(latencyMs) ms", icon: "timer")
            metric("Bandwidth", "\(bandwidthKbps) kbps", icon: "arrow.left.and.right")
            metric("FPS", "\(framesPerSecond)", icon: "camera.shutter.button")
        }
        .padding(.horizontal)
    }

    private func metric(_ title: String, _ value: String, icon: String) -> some View {
        VStack(alignment: .leading, spacing: 3) {
            Label(title, systemImage: icon)
                .font(.caption)
                .foregroundStyle(.secondary)
            Text(value)
                .font(.subheadline.weight(.semibold))
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(10)
        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 10, style: .continuous))
    }
}
