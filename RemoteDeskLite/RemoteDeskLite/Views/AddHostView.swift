import SwiftUI

struct AddHostView: View {
    @Environment(\.dismiss) private var dismiss

    let existingHost: RemoteHost?
    let onSave: (RemoteHost) -> Void

    @State private var name: String
    @State private var address: String
    @State private var protocolType: RemoteProtocol
    @State private var port: String
    @State private var username: String
    @State private var notes: String
    @State private var autoReconnect: Bool
    @State private var showValidationError = false

    init(existingHost: RemoteHost? = nil, onSave: @escaping (RemoteHost) -> Void) {
        self.existingHost = existingHost
        self.onSave = onSave

        _name = State(initialValue: existingHost?.name ?? "")
        _address = State(initialValue: existingHost?.address ?? "")
        _protocolType = State(initialValue: existingHost?.protocolType ?? .rdp)
        _port = State(initialValue: String(existingHost?.port ?? RemoteProtocol.rdp.defaultPort))
        _username = State(initialValue: existingHost?.username ?? "")
        _notes = State(initialValue: existingHost?.notes ?? "")
        _autoReconnect = State(initialValue: existingHost?.autoReconnect ?? true)
    }

    var body: some View {
        NavigationStack {
            Form {
                Section("Connection") {
                    TextField("Friendly name", text: $name)
                    TextField("Host / IP address", text: $address)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled(true)

                    Picker("Protocol", selection: $protocolType) {
                        ForEach(RemoteProtocol.allCases) { protocolType in
                            Text(protocolType.rawValue).tag(protocolType)
                        }
                    }
                    .onChange(of: protocolType) { _, newValue in
                        if let parsedPort = Int(port), RemoteProtocol.allCases.contains(where: { $0.defaultPort == parsedPort }) {
                            port = String(newValue.defaultPort)
                        }
                    }

                    TextField("Port", text: $port)
                        .keyboardType(.numberPad)
                }

                Section("Credentials") {
                    TextField("Username", text: $username)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled(true)
                }

                Section("Behavior") {
                    Toggle("Auto reconnect", isOn: $autoReconnect)
                }

                Section("Notes") {
                    TextField("Optional notes", text: $notes, axis: .vertical)
                        .lineLimit(3...6)
                }

                if showValidationError {
                    Section {
                        Text("Please provide name, address, and a valid port (1-65535).")
                            .font(.footnote)
                            .foregroundStyle(.red)
                    }
                }
            }
            .navigationTitle(existingHost == nil ? "New Host" : "Edit Host")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") {
                        save()
                    }
                }
            }
        }
    }

    private func save() {
        guard let resolvedPort = Int(port), (1...65_535).contains(resolvedPort) else {
            showValidationError = true
            return
        }

        let trimmedName = name.trimmingCharacters(in: .whitespacesAndNewlines)
        let trimmedAddress = address.trimmingCharacters(in: .whitespacesAndNewlines)

        guard !trimmedName.isEmpty, !trimmedAddress.isEmpty else {
            showValidationError = true
            return
        }

        let host = RemoteHost(
            id: existingHost?.id ?? UUID(),
            name: trimmedName,
            address: trimmedAddress,
            port: resolvedPort,
            protocolType: protocolType,
            username: username.trimmingCharacters(in: .whitespacesAndNewlines),
            notes: notes.trimmingCharacters(in: .whitespacesAndNewlines),
            autoReconnect: autoReconnect,
            lastConnectedAt: existingHost?.lastConnectedAt
        )

        onSave(host)
        dismiss()
    }
}
