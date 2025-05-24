let webSocket = null;
let dotNetRef = null;
let reconnectAttempts = 0;
let maxReconnectAttempts = 5;
let reconnectDelay = 2000;

export function connectWebSocket(dotNetReference, serverUrl) {
    dotNetRef = dotNetReference;

    try {
        console.log(`🔌 Connecting to WebSocket server: ${serverUrl}`);

        webSocket = new WebSocket(serverUrl);

        webSocket.onopen = function (event) {
            console.log('✅ WebSocket connection opened');
            reconnectAttempts = 0;
            notifyStatusChange('Connected');
        };

        webSocket.onmessage = function (event) {
            console.log('📨 WebSocket message received:', event.data);

            try {
                // Forward message to Blazor component
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnWebSocketMessage', event.data);
                }
            } catch (error) {
                console.error('❌ Error processing WebSocket message:', error);
            }
        };

        webSocket.onclose = function (event) {
            console.log('📪 WebSocket connection closed:', event.code, event.reason);
            notifyStatusChange('Disconnected');

            // Attempt to reconnect
            if (reconnectAttempts < maxReconnectAttempts) {
                setTimeout(() => {
                    reconnectAttempts++;
                    console.log(`🔄 Attempting to reconnect (${reconnectAttempts}/${maxReconnectAttempts})...`);
                    connectWebSocket(dotNetRef, serverUrl);
                }, reconnectDelay);
            } else {
                console.log('❌ Max reconnection attempts reached');
                notifyStatusChange('Connection Failed');
            }
        };

        webSocket.onerror = function (error) {
            console.error('❌ WebSocket error:', error);
            notifyStatusChange('Error');
        };

        return true;
    } catch (error) {
        console.error('❌ Failed to create WebSocket connection:', error);
        notifyStatusChange('Connection Failed');
        return false;
    }
}

export function sendMessage(message) {
    if (webSocket && webSocket.readyState === WebSocket.OPEN) {
        try {
            webSocket.send(message);
            console.log('📤 WebSocket message sent:', message);
            return true;
        } catch (error) {
            console.error('❌ Error sending WebSocket message:', error);
            return false;
        }
    } else {
        console.warn('⚠️ WebSocket is not connected. Cannot send message.');
        return false;
    }
}

export function disconnect() {
    if (webSocket) {
        try {
            console.log('🔌 Disconnecting WebSocket...');
            webSocket.close(1000, 'Client disconnect');
            webSocket = null;
        } catch (error) {
            console.error('❌ Error disconnecting WebSocket:', error);
        }
    }
}

export function getConnectionState() {
    if (!webSocket) return 'None';

    switch (webSocket.readyState) {
        case WebSocket.CONNECTING:
            return 'Connecting';
        case WebSocket.OPEN:
            return 'Open';
        case WebSocket.CLOSING:
            return 'Closing';
        case WebSocket.CLOSED:
            return 'Closed';
        default:
            return 'Unknown';
    }
}

function notifyStatusChange(status) {
    console.log(`📡 WebSocket status changed: ${status}`);

    try {
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnWebSocketStatusChange', status);
        }
    } catch (error) {
        console.error('❌ Error notifying status change:', error);
    }
}

// Utility function for sending structured messages
export function sendStructuredMessage(type, data) {
    const message = {
        type: type,
        data: data,
        timestamp: new Date().toISOString()
    };

    return sendMessage(JSON.stringify(message));
}

// Keep-alive ping function
export function sendPing() {
    return sendStructuredMessage('Ping', 'ping');
}

// Subscribe to flight updates
export function subscribeToFlight(flightNumber) {
    return sendStructuredMessage('FlightSubscription', { flightNumber: flightNumber });
}

// Send test message
export function sendTestMessage() {
    return sendStructuredMessage('Ping', 'Test message from web interface');
}

// Auto-reconnect with exponential backoff
function scheduleReconnect() {
    if (reconnectAttempts < maxReconnectAttempts) {
        const delay = reconnectDelay * Math.pow(2, reconnectAttempts);
        console.log(`🔄 Scheduling reconnect in ${delay}ms...`);

        setTimeout(() => {
            if (!webSocket || webSocket.readyState === WebSocket.CLOSED) {
                reconnectAttempts++;
                // Note: You'll need to store the server URL for reconnection
                // This is a simplified version
                notifyStatusChange('Reconnecting...');
            }
        }, delay);
    }
}