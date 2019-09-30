import React, { Component } from 'react';
import { Button, FormGroup, Label, Input } from 'reactstrap';

export class AlphaInit extends Component {
    static displayName = AlphaInit.name;

    constructor(props) {
        super(props);
        this.state = {
            donorSecret: "",
            auditors: "GC2Z2GVUXJFGU3SIC6S2RVVH35TUSNL7ZQPZXGTHEBFFNMMEGPIBTLTT",
            minAccountBalance: 100,
            minAllowedLotSize: 100000,
            assets: `XLM:0
USD-GAWJ5J3GRGHDRTQSJSUNLIBBRB47YMUWTS7TCNGIUWMJUML22Q2BJYFJ-1:1`,
            loading: false
        };
    }

    renderAlphaInitForm() {
        const { donorSecret, auditors, minAccountBalance, minAllowedLotSize, assets } = this.state
        return (
            <div>
                <FormGroup>
                    <Label for="donorSecret">Donor secret</Label>
                    <Input required name="donorSecret" value={donorSecret} onChange={(e) => this.setState({ donorSecret: e.target.value })} />
                </FormGroup>
                <FormGroup>
                    <Label for="auditors">Auditors (separete with comas)</Label>
                    <Input required name="auditors" value={auditors} onChange={(e) => this.setState({ auditors: e.target.value })} />
                </FormGroup>
                <FormGroup>
                    <Label for="minAccountBalance">Min Account Balance</Label>
                    <Input name="minAccountBalance" type="number" value={minAccountBalance} onChange={(e) => this.setState({ minAccountBalance: Number(e.target.value) })} />
                </FormGroup>
                <FormGroup>
                    <Label for="minAllowedLotSize">Min Allowed Lot Size</Label>
                    <Input name="minAllowedLotSize" type="number" value={minAllowedLotSize} onChange={(e) => this.setState({ minAllowedLotSize: Number(e.target.value) })} />
                </FormGroup>
                <FormGroup>
                    <Label for="assets">Supported assets (format: asset-symbol:assetId; put every new asset to the new line)</Label>
                    <Input required type="textarea" name="assets" value={assets} onChange={(e) => this.setState({ assets: e.target.value })} />
                </FormGroup>
                <Button onClick={() => this.submit()}>Submit</Button>
            </div>
        );
    }

    render() {
        let contents = this.state.loading
            ? <p><em>Submiting...</em></p>
            : this.renderAlphaInitForm(this.state.alphaInitData);

        return (
            <div>
                <h4>Alpha init form</h4>
                {contents}
            </div>
        );
    }

    async submit() {


        this.setState({ loading: true });

        const { donorSecret, auditors, minAccountBalance, minAllowedLotSize, assets } = this.state

        let supportedAssetsDict = {}
        const splittedAssets = assets.split('\n')
        for (let i = 0; i < splittedAssets.length; i++) {
            let asset = splittedAssets[i].split(':')
            if (asset[0])
                supportedAssetsDict[asset[0]] = Number(asset[1])
        }

        const init = {
            DonorSecret: donorSecret,
            auditors: auditors.split(','),
            minAccountBalance,
            minAllowedLotSize,
            assets: supportedAssetsDict
        }

        const response = await fetch('api/Alpha/Init', {
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json'
            },
            method: "POST",
            body: JSON.stringify(init)
        });
        const data = await response.json();
        this.setState({ loading: false });
        if (data.isSuccess)
            this.props.history.push('/')
        else {
            console.error(data.error)
            alert("Error occurred. See console for details")
        }
    }
}
