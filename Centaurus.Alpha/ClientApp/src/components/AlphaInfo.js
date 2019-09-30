import React, { Component } from 'react';


const alphaState = {

    "0": "Undefined",
    "1": "WaitingForInit",
    "2": "Running",
    "3": "Ready",
    "4": "Rising",
    "10": "Failed"
}

export class AlphaInfo extends Component {
    static displayName = AlphaInfo.name;

    constructor(props) {
        super(props);
        this.state = {
            alphaInfo: {},
            loading: true
        };
    }

    componentDidMount() {
        this.getAlphaInfo();
    }

    static renderAlphaInfo(alphaInfo) {
        return (
            <div>
                <table className='table table-striped'>
                    <thead>
                        <tr>
                            <th>Key</th>
                            <th>Value</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr>
                            <td>State</td>
                            <td>{alphaState[alphaInfo.state]}</td>
                        </tr>
                        <tr>
                            <td>Vault</td>
                            <td>{alphaInfo.vault}</td>
                        </tr>
                        <tr>
                            <td>Auditors</td>
                            <td>{alphaInfo.auditors && alphaInfo.auditors.join('; ')}</td>
                        </tr>
                        <tr>
                            <td>Stellar Network</td>
                            <td>{alphaInfo.stellarNetwork && (alphaInfo.stellarNetwork.horizon + '; ' + alphaInfo.stellarNetwork.passphrase)}</td>
                        </tr>
                        <tr>
                            <td>Supported Assets</td>
                            <td>{alphaInfo.assets && alphaInfo.assets.map(a => a.code + ':' + a.issuer).join('; ')}</td>
                        </tr>
                        <tr>
                            <td>State</td>
                            <td>{alphaInfo.minAccountBalance}</td>
                        </tr>
                        <tr>
                            <td>State</td>
                            <td>{alphaInfo.minAllowedLotSize}</td>
                        </tr>
                    </tbody>
                </table>
                {alphaInfo.state == 1 && <a href="/init">Init Alpha</a>}
            </div>
        );
    }

    render() {
        let contents = this.state.loading
            ? <p><em>Loading...</em></p>
            : AlphaInfo.renderAlphaInfo(this.state.alphaInfo);

        return (
            <div>
                <h4>Alpha server info</h4>
                {contents}
            </div>
        );
    }

    async getAlphaInfo() {
        const response = await fetch('api/Alpha/Info');
        const data = await response.json();
        this.setState({ alphaInfo: data, loading: false });
    }
}
