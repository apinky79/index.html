import SwiftUI

struct HostRowView: View {
    let host: RemoteHost

    var body: some View {
        HStack(spacing: 12) {
            VStack(alignment: .leading, spacing: 4) {
                HStack {
                    Text(host.name)
                        .font(.headline)
                    Spacer()
                    Text(host.protocolType.rawValue)
                        .font(.caption.bold())
                        .padding(.horizontal, 8)
                        .padding(.vertical, 4)
                        .background(.blue.opacity(0.15), in: Capsule())
                }

                Text(host.endpoint)
                    .font(.subheadline)
                    .foregroundStyle(.secondary)

                if let lastConnected = host.lastConnectedAt {
                    Text("Last connected \(lastConnected.formatted(date: .abbreviated, time: .shortened))")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }

            Image(systemName: "chevron.right")
                .font(.caption.bold())
                .foregroundStyle(.tertiary)
        }
        .padding(.vertical, 6)
    }
}
