import React, { Component } from 'react';
import { AlphaInfo } from './AlphaInfo'

export class Home extends Component {
    static displayName = Home.name;

    render() {
        return (
            <div>
                <h1>Welcome to Centaurus!</h1>
                <AlphaInfo/>
            </div>
        );
    }
}
